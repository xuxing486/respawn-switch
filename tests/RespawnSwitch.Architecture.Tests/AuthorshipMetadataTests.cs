// Author: Stress Monster
using System.Reflection;

namespace RespawnSwitch.Architecture.Tests;

public sealed class AuthorshipMetadataTests
{
    [Fact]
    public void Product_assemblies_preserve_the_Stress_Monster_signature()
    {
        var assembly = typeof(AuthorshipMetadataTests).Assembly;

        Assert.Equal("Stress Monster", assembly.GetCustomAttribute<AssemblyCompanyAttribute>()?.Company);
        Assert.Contains("Stress Monster", assembly.GetCustomAttribute<AssemblyCopyrightAttribute>()?.Copyright);
    }
}
