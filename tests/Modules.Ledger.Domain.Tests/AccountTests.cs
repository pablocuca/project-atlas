using Atlas.Kernel;
using Atlas.Modules.Ledger.Domain;

namespace Modules.Ledger.Domain.Tests;

public class AccountTests
{
    [Fact]
    [BusinessRule("BR-105")]
    public void Account_type_cannot_change_after_opening()
    {
        var account = Account.Open(
            TenantId.New(), "1.1.01", "Checking", AccountType.Asset, Commodity.Brl, parentId: null, TestSupport.Day1).Value;

        var closed = account.Close(TestSupport.Day2, Money.Zero(Commodity.Brl)).Value;

        // There is no method on Account that can change Type — closing it is the only mutation
        // path this type exposes, and it leaves Type untouched.
        Assert.Equal(AccountType.Asset, closed.Type);
        Assert.Equal(account.Type, closed.Type);
    }

    [Fact]
    [BusinessRule("BR-106")]
    public void Account_cannot_close_with_a_non_zero_balance()
    {
        var account = Account.Open(
            TenantId.New(), "1.1.01", "Checking", AccountType.Asset, Commodity.Brl, parentId: null, TestSupport.Day1).Value;

        var result = account.Close(TestSupport.Day2, Money.FromMinorUnits(1, Commodity.Brl));

        Assert.True(result.IsFailure);
        Assert.Equal("LEDGER.ACCOUNT_NON_ZERO_BALANCE", result.Error.Code);
    }

    [Fact]
    [BusinessRule("BR-106")]
    public void Account_closes_with_a_zero_balance()
    {
        var account = Account.Open(
            TenantId.New(), "1.1.01", "Checking", AccountType.Asset, Commodity.Brl, parentId: null, TestSupport.Day1).Value;

        var result = account.Close(TestSupport.Day2, Money.Zero(Commodity.Brl));

        Assert.True(result.IsSuccess);
        Assert.Equal(TestSupport.Day2, result.Value.ClosedAt);
    }

    [Fact]
    public void Open_fails_when_code_is_blank()
    {
        var result = Account.Open(TenantId.New(), "  ", "Checking", AccountType.Asset, Commodity.Brl, null, TestSupport.Day1);

        Assert.True(result.IsFailure);
        Assert.Equal("LEDGER.ACCOUNT_CODE_REQUIRED", result.Error.Code);
    }
}
