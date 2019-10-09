using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using NExpect;
using PeanutButter.Utils;
using static NExpect.Expectations;
using static PeanutButter.RandomGenerators.RandomValueGen;

namespace shared.tests
{
    [TestFixture]
    public class TestArgs
    {
        [TestFixture]
        public class ReadFlag
        {
            [Test]
            public void ShouldReturnFalseWhenNoFlag()
            {
                // Arrange
                var args = new List<string> { "foo", "bar" };
                var search = new[] { "--param", "-p" };
                // Act
                var result = args.ReadFlag(search);
                // Assert
                Expect(result)
                    .To.Be.False();
            }

            [Test]
            public void ShouldReturnTrueWhenMatchesOne()
            {
                // Arrange
                var search = new[] { "--param", "-p" };
                var args = new List<string> { "foo", "bar" };
                var toFind = GetRandomFrom(search);
                args.Add(toFind);
                // Act
                var result = args.ReadFlag(search[0], search[1]);
                // Assert
                Expect(result)
                    .To.Be.True();
                Expect(args)
                    .Not.To.Contain.Any()
                    .Equal.To(toFind);
            }
        }

        [TestFixture]
        public class ReadParameter
        {
            [Test]
            public void ShouldReturnEmptyWhenNoParameter()
            {
                // Arrange
                var args = new List<string>() { "-a", "-b" };
                var before = args.DeepClone();
                var search = new[] { "--search", "-s" };
                // Act
                var result = args.ReadParameter(GetRandom<ParameterOptions>(), search);
                // Assert
                Expect(result).To.Be.Empty();
                Expect(args).To.Equal(before);
            }

            [TestFixture]
            public class WhenNotGreedy
            {
                [Test]
                public void ShouldReturnSingleValueFromSingleParameter()
                {
                    // Arrange
                    var args = new List<string>() { "-f", "filename" };
                    // Act
                    var result = args.ReadParameter(
                        ParameterOptions.Conservative,
                        "-f", "--file");
                    // Assert
                    Expect(result).To.Equal(
                        new[] { "filename" }
                    );
                    Expect(args).To.Be.Empty();
                }

                [Test]
                public void ShouldReturnSingleValueFromSingleParameterWithTrailingValue()
                {
                    // Arrange
                    var args = new List<string>() { "-f", "filename", "another" };
                    // Act
                    var result = args.ReadParameter(
                        ParameterOptions.Conservative,
                        "-f", "--file"
                    );
                    // Assert
                    Expect(result).To.Equal(
                        new[] { "filename" }
                    );
                    Expect(args).To.Equal(
                        new[] { "another" }
                    );
                }

                [Test]
                public void ShouldReturnMultipleValuesFromMultipleParametersWithTrailingValues()
                {
                    // Arrange
                    var args = new List<string>()
                    {
                        "-f", "file1", "lost1",
                        "--file", "file2", "lost2"
                    };
                    // Act
                    var result = args.ReadParameter(
                        ParameterOptions.Conservative,
                        "-f", "--file"
                    );
                    // Assert
                    Expect(result).To.Equal(
                        new[] { "file1", "file2" }
                    );
                    Expect(args).To.Equal(
                        new[] { "lost1", "lost2" }
                    );
                }
            }

            [TestFixture]
            public class WhenGreedy
            {
                [Test]
                public void ShouldReturnEmptyWhenParameterMissing()
                {
                    // Arrange
                    var args = new List<string>()
                    {
                        "file1", "file2"
                    };
                    var snapshot = args.DeepClone();
                    // Act
                    var result = args.ReadParameter(
                        ParameterOptions.Greedy,
                        "-f", "--file"
                    );
                    // Assert
                    Expect(result).To.Be.Empty();
                    Expect(args).To.Equal(snapshot);
                }

                [Test]
                public void ShouldReturnEmptyWhenParameterValueMissing()
                {
                    // Arrange
                    var args = new List<string>()
                    {
                        "file1", "-f"
                    };
                    // Act
                    var result = args.ReadParameter(
                        ParameterOptions.Greedy,
                        "-f", "--file"
                    );

                    // Assert
                    Expect(result).To.Be.Empty();
                    Expect(args).To.Equal(new[] { "file1" });
                }

                [Test]
                public void ShouldEatAllParametersUntilNextParameter()
                {
                    // Arrange
                    var parameter = GetRandomFrom(new[] { "-f", "--file" });
                    var args = new List<string>()
                    {
                        parameter, "file1", "file2",
                        "-p", "parameter",
                    };
                    // Act
                    var result = args.ReadParameter(
                        ParameterOptions.Greedy,
                        "-f", "--file"
                    );
                    // Assert
                    Expect(result).To.Equal(
                        new[] { "file1", "file2" }
                    );
                    Expect(args).To.Equal(
                        new[] { "-p", "parameter" }
                    );
                }

                [Test]
                public void ShouldEatAllParametersUntilNextParameterWhenMultiple()
                {
                    // Arrange
                    var parameters = new[] { "-f", "--file" };
                    var parameter1 = "--file"; //GetRandomFrom(parameters);
                    var parameter2 = "--file"; // GetRandomFrom(parameters);
                    Console.WriteLine(
                        $"params: {parameter1} / {parameter2}"
                    );
                    var args = new List<string>()
                    {
                        parameter1, "file1", "file2",
                        "-p", "parameter",
                        parameter2, "file3", "file4", "file5"
                    };
                    // Act
                    var result = args.ReadParameter(
                        ParameterOptions.Greedy,
                        "-f", "--file"
                    );
                    // Assert
                    Expect(result).To.Equal(
                        new[] { "file1", "file2", "file3", "file4", "file5" }
                    );
                    Expect(args).To.Equal(
                        new[] { "-p", "parameter" }
                    );
                }

                [Test]
                public void ShouldRespectTerminator()
                {
                    // Arrange
                    var parameters = new[] { "-f", "--file" };
                    var parameter1 = GetRandomFrom(parameters);
                    var args = new List<string>()
                    {
                        parameter1, "file1", "file2",
                        "--", "parameter",
                    };
                    // Act
                    var result = args.ReadParameter(
                        ParameterOptions.Greedy,
                        "-f", "--file"
                    );
                    // Assert
                    Expect(result).To.Equal(
                        new[] { "file1", "file2" }
                    );
                    Expect(args).To.Equal(
                        new[] { "--", "parameter" }
                    );
                }

                [Test]
                public void ShouldNotStealValuesFromOtherParameters()
                {
                    // Arrange
                    var args = new List<string>()
                    {
                        "-f", "file1", "file2",
                        "-a", "file2", "file3",
                        "-p", "file1", "file3"
                    };
                    // Act
                    var result1 = args.ReadParameter(
                        ParameterOptions.Greedy,
                        "--file", "-f"
                    );
                    var result2 = args.ReadParameter(
                        ParameterOptions.Greedy,
                        "-p", "--prop"
                    );
                    // Assert
                    Expect(result1).To.Equal(
                        new[] { "file1", "file2" }
                    );
                    Expect(result2).To.Equal(
                        new[] { "file1", "file3" }
                    );
                    Expect(args).To.Equal(
                        new[] { "-a", "file2", "file3" }
                    );
                }
            }
        }
    }
}