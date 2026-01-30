using FluentAssertions;
using NUnit.Framework;

namespace TemplateDotnetCoreConsoleApp.Cmd.Tests;

public class DefaultCommandTests
{
  [Test]
  public async Task ExecuteAsync_GivenDefaultSettings_ShouldReturn1()
  {
    // arrange
    var defaultCommandTests = new DefaultCommand();
    // action
    var executeAsync = await defaultCommandTests.ExecuteAsync(null!, new DefaultCommand.Settings());
    // assert
    executeAsync.Should().Be(1);
    

  }

  [Test]
  public async Task ExecuteAsync_GivenDefaultSettingsWithName_ShouldReturn0()
  {
    // arrange
    var defaultCommandTests = new DefaultCommand();
    // action
    var executeAsync =
      await defaultCommandTests.ExecuteAsync(null!, new DefaultCommand.Settings() { Name = "Hi", Table = true });
    // assert
    executeAsync.Should().Be(0);
  }
}
