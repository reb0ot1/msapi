using Microsoft.VisualStudio.TestPlatform.ObjectModel.Client;
using WebApplicationTeamCity.Services;
using Xunit.Sdk;

namespace WebApplicationTeamCity.Tests
{
    // Fix: Create a concrete attribute class derived from BeforeAfterTestAttribute
    public class CustomBeforeAfterTestAttribute : BeforeAfterTestAttribute
    {
        public override void Before(System.Reflection.MethodInfo methodUnderTest)
        {
            Console.WriteLine("Before test execution");
        }

        public override void After(System.Reflection.MethodInfo methodUnderTest)
        {
            Console.WriteLine("After test execution");
        }
    }

    public class UnitTest1
    {
        //[CustomBeforeAfterTest] // Use the concrete attribute instead of the abstract one
        [Fact]
        public void Test1()
        {
            var service = new TestService();

            var result = service.GetTestData();

            Assert.Equal("This is a test data from a test service", result);
        }

        [Fact]
        public void Test2()
        {
            var service = new TestService();

            var result = service.GetTestData();

            Assert.Equal("This is a test data from a test service 2", result);
        }
    }
}
