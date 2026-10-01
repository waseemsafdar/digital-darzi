using Domain.Entities;
using FluentAssertions;
using Infrastructure.Services;
using Xunit;

namespace DigitalDarzi.UnitTests.Finance;

public class SalaryCalculatorTests
{
    private static SalaryRule Rule(decimal monthlySalary = 26000m, int workingDays = 26, int paidLeaveDays = 1, decimal halfDayFactor = 0.5m)
        => new() { MonthlySalary = monthlySalary, WorkingDaysPerMonth = workingDays, PaidLeaveDays = paidLeaveDays, HalfDayFactor = halfDayFactor };

    // Rule: per-day rate = monthly salary ÷ working days per month.
    // Why:  a wrong per-day rate under- or over-deducts every absence, which compounds into a
    //       real underpayment or overcharge on the karigar's monthly pay — the number staff and
    //       the karigar both check first when a payslip looks wrong.
    [Fact]
    public void Compute_PerDayRate_IsMonthlySalaryDividedByWorkingDays()
    {
        var result = SalaryCalculator.Compute(Rule(monthlySalary: 26000m, workingDays: 26), present: 26, halfDays: 0, leave: 0, absent: 0, outstandingAdvances: 0);

        result.PerDayRate.Should().Be(1000m);
    }

    // Rule: unpaid days = absences + half-days x (1 - half-day factor) + leave beyond the paid allowance.
    // Why:  a wrong mix here would show up as a wrong number on a real karigar's payslip — the
    //       first thing they and the shop owner both check when a month's pay looks off.
    [Fact]
    public void Compute_MixOfAbsenceTypes_DeductsExpectedAmount()
    {
        // 3 absent + 2 half-days at 0.5 factor (=1 unpaid day) + 1 unpaid leave day (2 taken - 1 paid) = 5 unpaid days
        var result = SalaryCalculator.Compute(Rule(paidLeaveDays: 1), present: 19, halfDays: 2, leave: 2, absent: 3, outstandingAdvances: 0);

        result.UnpaidDays.Should().Be(5m);
        result.AttendanceDeduction.Should().Be(5000m, "5 unpaid days x Rs 1000/day");
        result.NetPayable.Should().Be(21000m, "Rs 26000 salary - Rs 5000 attendance deduction");
    }

    [Fact]
    public void Compute_LeaveWithinPaidAllowance_NoDeductionForThoseDays()
        // 1 leave day taken, 3 paid allowed — comfortably within the allowance (distinct from
        // the exactly-at-the-boundary case below).
        => SalaryCalculator.Compute(Rule(paidLeaveDays: 3), present: 25, halfDays: 0, leave: 1, absent: 0, outstandingAdvances: 0)
            .AttendanceDeduction.Should().Be(0m);

    [Fact]
    public void Compute_LeaveExactlyAtPaidAllowance_BoundaryIsInclusive()
        // Leave == PaidLeaveDays exactly: Math.Max(0, leave - paid) = 0, no deduction. Proves the
        // boundary is <= (paid covers exactly this many days), not < (which would deduct one day here).
        => SalaryCalculator.Compute(Rule(paidLeaveDays: 3), present: 23, halfDays: 0, leave: 3, absent: 0, outstandingAdvances: 0)
            .UnpaidDays.Should().Be(0m);

    [Fact]
    public void Compute_LeaveOneOverPaidAllowance_DeductsExactlyOneDay()
        => SalaryCalculator.Compute(Rule(paidLeaveDays: 3, monthlySalary: 26000m, workingDays: 26), present: 22, halfDays: 0, leave: 4, absent: 0, outstandingAdvances: 0)
            .AttendanceDeduction.Should().Be(1000m);

    // Half-day factor: 0 = a half-day costs a full day's pay; 1 = a half-day costs nothing.
    [Fact]
    public void Compute_HalfDayFactorZero_HalfDayCostsFullDay()
        => SalaryCalculator.Compute(Rule(halfDayFactor: 0m), present: 25, halfDays: 1, leave: 0, absent: 0, outstandingAdvances: 0)
            .AttendanceDeduction.Should().Be(1000m);

    [Fact]
    public void Compute_HalfDayFactorOne_HalfDayCostsNothing()
        => SalaryCalculator.Compute(Rule(halfDayFactor: 1m), present: 25, halfDays: 1, leave: 0, absent: 0, outstandingAdvances: 0)
            .AttendanceDeduction.Should().Be(0m);

    // Rule: the attendance deduction can never exceed the monthly salary itself.
    // Why:  without this cap, a staff member absent for an unusually long stretch (more unpaid
    //       days than the month has working days) would compute a NEGATIVE net-before-advances,
    //       which AttendanceDeduction is supposed to prevent from ever appearing on a payslip.
    [Fact]
    public void Compute_DeductionExceedingSalary_IsCappedAtMonthlySalary()
        => SalaryCalculator.Compute(Rule(monthlySalary: 5000m, workingDays: 10), present: 0, halfDays: 0, leave: 0, absent: 30, outstandingAdvances: 0)
            .AttendanceDeduction.Should().Be(5000m);

    // Rule: NetPayable = monthly salary - attendance deduction - outstanding advances, never below zero.
    // Why:  if a karigar's advances exceed what they earned this month, showing a negative
    //       payslip would be nonsensical; the shop carries the remainder forward instead.
    [Fact]
    public void Compute_AdvancesExceedingRemainingSalary_ClampsNetPayableToZero()
    {
        var result = SalaryCalculator.Compute(Rule(monthlySalary: 5000m), present: 26, halfDays: 0, leave: 0, absent: 0, outstandingAdvances: 8000m);

        result.NetPayable.Should().Be(0m);
        result.AdvancesToRecover.Should().Be(8000m, "the full outstanding advance is still reported even though it can't all be recovered this month");
    }

    [Fact]
    public void Compute_NoAbsencesNoAdvances_NetPayableEqualsMonthlySalary()
        => SalaryCalculator.Compute(Rule(monthlySalary: 26000m), present: 26, halfDays: 0, leave: 0, absent: 0, outstandingAdvances: 0)
            .NetPayable.Should().Be(26000m);

    // Rule: the per-day rate and the deduction are each rounded to 2 decimal places independently.
    // Why:  25000 / 26 working days does not divide evenly (961.5384...). Every fixture above
    //       uses an even division and would stay green even if rounding were dropped entirely;
    //       this pins the actual rounded figures so a payslip never shows a fraction of a paisa.
    [Fact]
    public void Compute_UnevenDivision_RoundsPerDayRateAndDeductionToTwoDecimals()
    {
        var result = SalaryCalculator.Compute(Rule(monthlySalary: 25000m, workingDays: 26, paidLeaveDays: 0), present: 13, halfDays: 0, leave: 0, absent: 13, outstandingAdvances: 0);

        result.PerDayRate.Should().Be(961.54m, "25000 / 26 = 961.5384... rounded to 2dp");
        result.AttendanceDeduction.Should().Be(12500.02m, "13 x 961.54, not 13 x the unrounded 961.5384...");
    }

    // Rule: a working-days-per-month of zero never divides by zero.
    // Why:  SaveSalaryRuleValidator already restricts the form field to 1-31, but this floor is
    //       what actually stops Compute itself from throwing if that value is ever zero anyway
    //       (an old row saved before the validator existed, or a future caller that skips it).
    [Fact]
    public void Compute_ZeroWorkingDaysConfigured_FloorsToOneToAvoidDivideByZero()
    {
        var act = () => SalaryCalculator.Compute(Rule(monthlySalary: 1000m, workingDays: 0), present: 1, halfDays: 0, leave: 0, absent: 0, outstandingAdvances: 0);

        act.Should().NotThrow();
        act().WorkingDaysPerMonth.Should().Be(1);
    }
}
