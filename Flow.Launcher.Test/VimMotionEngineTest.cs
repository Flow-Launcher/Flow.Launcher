using NUnit.Framework;
using Flow.Launcher.VimMode;

namespace Flow.Launcher.Test
{
    [TestFixture]
    public class VimMotionEngineTest
    {
        private const string Sample = "hello world_foo bar";

        [Test]
        public void MoveLeftTest()
        {
            Assert.That(VimMotionEngine.MoveLeft(5, Sample.Length), Is.EqualTo(4));
            Assert.That(VimMotionEngine.MoveLeft(0, Sample.Length), Is.EqualTo(0));
            Assert.That(VimMotionEngine.MoveLeft(2, Sample.Length), Is.EqualTo(1));
        }

        [Test]
        public void MoveRightTest()
        {
            Assert.That(VimMotionEngine.MoveRight(5, Sample.Length), Is.EqualTo(6));
            Assert.That(VimMotionEngine.MoveRight(Sample.Length, Sample.Length), Is.EqualTo(Sample.Length));
            Assert.That(VimMotionEngine.MoveRight(Sample.Length - 1, Sample.Length), Is.EqualTo(Sample.Length));
        }

        [Test]
        public void MoveStartOfLineTest()
        {
            Assert.That(VimMotionEngine.MoveStartOfLine(), Is.EqualTo(0));
        }

        [Test]
        public void MoveEndOfLineTest()
        {
            Assert.That(VimMotionEngine.MoveEndOfLine(Sample.Length), Is.EqualTo(Sample.Length));
            Assert.That(VimMotionEngine.MoveEndOfLine(0), Is.EqualTo(0));
        }

        [Test]
        public void MoveFirstNonBlankTest()
        {
            Assert.That(VimMotionEngine.MoveFirstNonBlank("   hello"), Is.EqualTo(3));
            Assert.That(VimMotionEngine.MoveFirstNonBlank("hello"), Is.EqualTo(0));
            Assert.That(VimMotionEngine.MoveFirstNonBlank("    "), Is.EqualTo(0));
            Assert.That(VimMotionEngine.MoveFirstNonBlank(""), Is.EqualTo(0));
        }

        [Test]
        public void MoveNextWordTest()
        {
            Assert.That(VimMotionEngine.MoveNextWord(Sample, 0), Is.EqualTo(6));
            Assert.That(VimMotionEngine.MoveNextWord(Sample, 6), Is.EqualTo(16));
            Assert.That(VimMotionEngine.MoveNextWord(Sample, Sample.Length), Is.EqualTo(Sample.Length));
            Assert.That(VimMotionEngine.MoveNextWord("   abc", 0), Is.EqualTo(3));
        }

        [Test]
        public void MovePrevWordTest()
        {
            Assert.That(VimMotionEngine.MovePrevWord(Sample, 6), Is.EqualTo(0));
            Assert.That(VimMotionEngine.MovePrevWord("hello   world", 8), Is.EqualTo(0));
            Assert.That(VimMotionEngine.MovePrevWord(Sample, 0), Is.EqualTo(0));
        }

        [Test]
        public void MoveEndWordTest()
        {
            Assert.That(VimMotionEngine.MoveEndWord(Sample, 0), Is.EqualTo(4));
            Assert.That(VimMotionEngine.MoveEndWord(Sample, 5), Is.EqualTo(14));
            Assert.That(VimMotionEngine.MoveEndWord(Sample, Sample.Length - 1), Is.EqualTo(Sample.Length));
        }

        [Test]
        public void FindCharForwardTest()
        {
            Assert.That(VimMotionEngine.FindCharForward(Sample, 0, 'o', false), Is.EqualTo(4));
            Assert.That(VimMotionEngine.FindCharForward(Sample, 0, 'o', till: true), Is.EqualTo(3));
            Assert.That(VimMotionEngine.FindCharForward(Sample, 0, 'z', false), Is.EqualTo(0));
            Assert.That(VimMotionEngine.FindCharForward(Sample, Sample.Length - 1, 'o', false), Is.EqualTo(Sample.Length - 1));
            Assert.That(VimMotionEngine.FindCharForward(Sample, 4, 'o', false), Is.EqualTo(7));
        }

        [Test]
        public void FindCharBackwardTest()
        {
            Assert.That(VimMotionEngine.FindCharBackward(Sample, 7, 'o', false), Is.EqualTo(4));
            Assert.That(VimMotionEngine.FindCharBackward(Sample, 7, 'o', till: true), Is.EqualTo(5));
            Assert.That(VimMotionEngine.FindCharBackward(Sample, Sample.Length - 1, 'z', false), Is.EqualTo(Sample.Length - 1));
            Assert.That(VimMotionEngine.FindCharBackward(Sample, 0, 'o', false), Is.EqualTo(0));
            Assert.That(VimMotionEngine.FindCharBackward(Sample, 4, 'o', false), Is.EqualTo(4));
        }
    }
}
