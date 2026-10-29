using LetsLearn.Core.Shared;

namespace LetsLearn.Test.Services;

public sealed class AppRolesTests
{
    [Theory]
    [InlineData("Student")]
    [InlineData("student")]
    [InlineData("Learner")]
    [InlineData("learner")]
    public void TreatsStudentAndLearnerNamesAsLearnerRole(string role)
    {
        Assert.True(AppRoles.IsStudent(role));
    }

    [Theory]
    [InlineData("Teacher")]
    [InlineData("Admin")]
    [InlineData("")]
    [InlineData(null)]
    public void DoesNotTreatOtherRolesAsLearner(string? role)
    {
        Assert.False(AppRoles.IsStudent(role));
    }
}
