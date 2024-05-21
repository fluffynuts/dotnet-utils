using NSubstitute;

namespace typedeps.tests;

[TestFixture]
public class TestDependencyWalker
{
    [TestFixture]
    public class ConstructorDependencies
    {
        [TestFixture]
        public class WhenTypeHasNoConstructorDependencies
        {
            [Test]
            public void ShouldReturnEmptyGraph()
            {
                // Arrange
                var sut = Create();
                // Act
                var result = sut.Walk<NoDeps>();
                // Assert
                Expect(result)
                    .Not.To.Be.Null();
                Expect(result.Name)
                    .To.Equal(nameof(NoDeps));
                Expect(result.Children)
                    .Not.To.Be.Null();
                Expect(result.Children)
                    .To.Be.Empty();
            }

            public class NoDeps;
        }

        [TestFixture]
        public class WhenTypeHasConstructorDependencies
        {
            [Test]
            public void ShouldReportLevel1()
            {
                // Arrange
                var sut = Create();
                // Act
                var result = sut.Walk<Service1>();
                // Assert
                Expect(result.Name)
                    .To.Equal(nameof(Service1));
                Expect(result.Children)
                    .To.Contain.Only(1).Item();
                var child = result.Children[0];
                Expect(child.Name)
                    .To.Start.With(nameof(Service2Implementation));
                Expect(child.Contracts)
                    .To.Contain(typeof(IService2));
            }

            [Test]
            public void ShouldIncludeImplementationWhenAvailable()
            {
                // Arrange
                // Arrange
                var sut = Create();
                // Act
                var result = sut.Walk<Service1>();
                // Assert
                Expect(result.Name)
                    .To.Equal(nameof(Service1));
                Expect(result.Children)
                    .To.Contain.Only(1).Item();
                var child = result.Children[0];
                Expect(child.Name)
                    .To.Start.With(nameof(Service2Implementation));
                Expect(child.Contracts)
                    .To.Contain(typeof(IService2));
                Expect(child.Implementation)
                    .To.Be(typeof(Service2Implementation));
            }

            [Test]
            public void ShouldReportCyclicDependency()
            {
                // Arrange
                var sut = Create();
                // Act
                var result = sut.Walk<Cyclic1>();
                // Assert
                Expect(result.Children)
                    .Not.To.Be.Empty();
                Expect(result.Children[0].Children)
                    .Not.To.Be.Empty();
                Expect(result.Children[0].Children[0].Name)
                    .To.Contain("cyclic");
                Expect(result.Children[0].Children[0].Children)
                    .To.Be.Empty();
            }

            public interface IService1
            {
            }

            public class Service1 : IService1
            {
                public Service1(IService2 service2)
                {
                }
            }

            public interface IService2
            {
            }

            public class Service2Implementation : IService2
            {
            }

            public interface ICyclic1;
            public interface ICyclic2;
            public class Cyclic1(ICyclic2 dep) : ICyclic1;
            public class Cyclic2(ICyclic1 dep): ICyclic2;
        }
    }

    private static DependencyWalker Create(
        IOptions? options = null
    )
    {
        if (options is null)
        {
            options = Substitute.For<IOptions>();
            options.NameStyle.Returns(NameStyles.Short);
        }

        return new(options);
    }
}