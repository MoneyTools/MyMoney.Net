using NUnit.Framework;
using Walkabout.Utilities;

namespace Walkabout.Tests
{
    public class FileHelperTests
    {
        [Test]
        public void TestRelativeUri()
        {
            var rel = FileHelpers.GetRelativePath(@"c:\temp\test\foo.xml", @"c:\temp\test\index.xml");
            Assert.AreEqual("foo.xml", rel);

            rel = FileHelpers.GetRelativePath(@"c:\temp\test\foo.xml", @"c:\temp\index.xml");
            Assert.AreEqual(@"test\foo.xml", rel);

            rel = FileHelpers.GetRelativePath(@"c:\temp\test\foo.xml", @"c:\index.xml");
            Assert.AreEqual(@"temp\test\foo.xml", rel);

            rel = FileHelpers.GetRelativePath(@"c:\temp\foo\foo.xml", @"c:\temp\bar\index.xml");
            Assert.AreEqual(@"..\foo\foo.xml", rel);

            rel = FileHelpers.GetRelativePath(@"c:\temp\foo\2\foo.xml", @"c:\temp\bar\1\index.xml");
            Assert.AreEqual(@"..\..\foo\2\foo.xml", rel);
        }
    }
}
