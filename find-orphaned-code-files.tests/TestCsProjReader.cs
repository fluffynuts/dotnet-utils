using System;
using System.IO;
using find_orphaned_code_files;
using NUnit.Framework;
using NExpect;
using PeanutButter.Utils;
using static NExpect.Expectations;
using static PeanutButter.RandomGenerators.RandomValueGen;

namespace Tests
{
    [TestFixture]
    public class TestCsProjReader
    {
        [TestFixture]
        public class Constructor
        {
            [Test]
            public void ShouldThrowFileNotFoundWhenPathNotFound()
            {
                using (var tempFolder = new AutoTempFolder())
                {
                    // Arrange
                    var csProj = Path.Combine(tempFolder.Path, "foo.csproj");
                    // Act
                    Expect(() => Create(csProj))
                        .To.Throw<FileNotFoundException>();
                    // Assert
                }
            }

            [Test]
            public void ShouldThrowIfBadFile()
            {
                // Arrange
                using (var tempFile = new AutoTempFile())
                {
                    tempFile.StringData = GetRandomString(100);
                    // Act
                    Expect(() => Create(tempFile.Path))
                        .To.Throw<NotSupportedException>();
                    // Assert
                }
            }

            [Test]
            public void ShouldThrowForModernProject()
            {
                // Arrange
                using (var tempFile = new AutoTempFile())
                {
                    // Act
                    tempFile.StringData = MODERN_CSPROJ.Trim();
                    // Assert
                    Expect(() => Create(tempFile.Path))
                        .To.Throw<NotSupportedException>();
                }
            }

            [Test]
            public void ShouldNotThrowForLegacyProject()
            {
                // Arrange
                using (var tempFile = new AutoTempFile())
                {
                    tempFile.StringData = LEGACY_CSPROJ.Trim();
                    // Act
                    Expect(() => Create(tempFile.Path))
                        .Not.To.Throw();
                   // Assert
                }
            }
        }

        [TestFixture]
        public class CompiledFiles
        {
            [Test]
            public void ShouldReturnAllFilesWhichAre_Compile_Include()
            {
                using (var tempFile = new AutoTempFile())
                {
                    // Arrange
                    tempFile.StringData = LEGACY_CSPROJ.Trim();
                    var sut = Create(tempFile.Path);
                    // Act
                    var result = sut.CompiledFiles;
                    // Assert
                    Expect(result).To.Equal(
                        new[]
                        {
                            "Commands\\Moo\\MooCommand.cs",
                            "Queries\\Cow\\CowQuery.cs",
                            "Base.cs"
                        });
                }
            }
        }

        [TestFixture]
        public class ContentFiles
        {
            [Test]
            public void ShouldReturnAllFilesWhichAre_Content_Include()
            {
                using (var tempFile = new AutoTempFile())
                {
                    // Arrange
                    tempFile.StringData = LEGACY_CSPROJ.Trim();
                    var sut = Create(tempFile.Path);
                    // Act
                    var result = sut.ContentFiles;
                    // Assert
                    Expect(result).To.Equal(
                        new[]
                        {
                            "README.md",
                            "GPL.txt"
                        });
                }
            }
        }

        private const string LEGACY_CSPROJ = @"
<?xml version=""1.0"" encoding=""utf-8""?>
<Project ToolsVersion=""15.0"" DefaultTargets=""Build"" xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
    <PropertyGroup>
        <LangVersion>latest</LangVersion>
        <TargetFrameworkVersion>v4.6.2</TargetFrameworkVersion>
        <ImplicitlyExpandNETStandardFacades>false</ImplicitlyExpandNETStandardFacades>
    </PropertyGroup>
    <ItemGroup>
        <Reference Include=""Analytics.NET, Version=2.0.3.0, Culture=neutral, processorArchitecture=MSIL"">
            <HintPath>..\packages\Analytics.3.0.0\lib\Analytics.NET.dll</HintPath>
        </Reference>
        <Reference Include=""BouncyCastle.Crypto, Version=1.8.3.0, Culture=neutral, PublicKeyToken=0e99375e54769942"">
            <HintPath>..\packages\BouncyCastle.1.8.3.1\lib\BouncyCastle.Crypto.dll</HintPath>
            <Private>True</Private>
        </Reference>
    </ItemGroup>
    <ItemGroup>
        <Compile Include=""Commands\Moo\MooCommand.cs"" />
        <Compile Include=""Queries\Cow\CowQuery.cs"" />
        <Compile Include=""Base.cs"" />
        <Content Include=""README.md"" />
        <Content Include=""GPL.txt"" />
    </ItemGroup>
</Project>
";

        private const string MODERN_CSPROJ = @"
<Project Sdk=""Microsoft.NET.Sdk"">
    <PropertyGroup>
        <Configuration Condition="" '$(Configuration)' == '' "">Debug</Configuration>
        <Platform Condition="" '$(Platform)' == '' "">AnyCPU</Platform>
    </PropertyGroup>
    <PropertyGroup>
        <TargetFramework>net462</TargetFramework>
        <IsPackable>false</IsPackable>
        <RootNamespace>Fake.Project</RootNamespace>
        <Configurations>Debug;Release;IsolatedTesting</Configurations>
        <Platforms>AnyCPU;x86</Platforms>
    </PropertyGroup>
    <PropertyGroup Condition="" '$(Configuration)' == 'IsolatedTesting' "">
        <DefineConstants>TRACE;DEBUG;ISOLATE_TESTS</DefineConstants>
        <OutputPath>bin\x86\Debug</OutputPath>
    </PropertyGroup>

    <ItemGroup>
        <PackageReference Include=""PeanutButter.RandomGenerators"" Version=""1.2.302"" />
        <PackageReference Include=""PeanutButter.TempDb.MySql.Data"" Version=""1.2.302"" />
        <PackageReference Include=""PeanutButter.Utils"" Version=""1.2.302"" />
        <PackageReference Include=""System.Collections"" Version=""4.3.0"" />
        <PackageReference Include=""System.Collections.Concurrent"" Version=""4.3.0"" />
        <PackageReference Include=""System.Collections.Immutable"" Version=""1.3.0"" />
        <PackageReference Include=""System.ValueTuple"" Version=""4.5.0"" />
    </ItemGroup>

    <ItemGroup>
        <ProjectReference Include=""..\BLL\BLL.csproj"" />
    </ItemGroup>
</Project>
";

        private static CsProjFileReader Create(string path)
        {
            return new CsProjFileReader(path);
        }
    }
}