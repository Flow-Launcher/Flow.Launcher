using NUnit.Framework;
using System.Collections.Generic;
using Flow.Launcher.VimMode;

namespace Flow.Launcher.Test
{
    [TestFixture]
    public class VimEngineTest
    {
        [Test]
        public void DefaultModeIsInsert()
        {
            var engine = new VimEngine();
            Assert.That(engine.CurrentMode, Is.EqualTo(VimModeType.Insert));
        }

        [Test]
        public void SwitchToInsertFromNormalFiresEvent()
        {
            var engine = new VimEngine();
            engine.SwitchToNormal();
            var modes = new List<VimModeType>();
            engine.ModeChanged += m => modes.Add(m);

            engine.SwitchToInsert();

            Assert.That(engine.CurrentMode, Is.EqualTo(VimModeType.Insert));
            Assert.That(modes, Is.EqualTo(new[] { VimModeType.Insert }));
        }

        [Test]
        public void SwitchToInsertWhenAlreadyInsertDoesNotFire()
        {
            var engine = new VimEngine();
            var fired = false;
            engine.ModeChanged += _ => fired = true;

            engine.SwitchToInsert();

            Assert.That(fired, Is.False);
        }

        [Test]
        public void SwitchToNormalFiresEvent()
        {
            var engine = new VimEngine();
            var modes = new List<VimModeType>();
            engine.ModeChanged += m => modes.Add(m);

            engine.SwitchToNormal();

            Assert.That(engine.CurrentMode, Is.EqualTo(VimModeType.Normal));
            Assert.That(modes, Is.EqualTo(new[] { VimModeType.Normal }));
        }

        [Test]
        public void SwitchToNormalWhenAlreadyNormalDoesNotFire()
        {
            var engine = new VimEngine();
            engine.SwitchToNormal();
            var fired = false;
            engine.ModeChanged += _ => fired = true;

            engine.SwitchToNormal();

            Assert.That(fired, Is.False);
        }

        [Test]
        public void SwitchToVisualFiresEvent()
        {
            var engine = new VimEngine();
            engine.SwitchToNormal();
            var modes = new List<VimModeType>();
            engine.ModeChanged += m => modes.Add(m);

            engine.SwitchToVisual();

            Assert.That(engine.CurrentMode, Is.EqualTo(VimModeType.Visual));
            Assert.That(modes, Is.EqualTo(new[] { VimModeType.Visual }));
        }

        [Test]
        public void SwitchToVisualWhenAlreadyVisualDoesNotFire()
        {
            var engine = new VimEngine();
            engine.SwitchToVisual();
            var fired = false;
            engine.ModeChanged += _ => fired = true;

            engine.SwitchToVisual();

            Assert.That(fired, Is.False);
        }

        [Test]
        public void SwitchToVisualLineFiresEvent()
        {
            var engine = new VimEngine();
            engine.SwitchToVisual();
            var modes = new List<VimModeType>();
            engine.ModeChanged += m => modes.Add(m);

            engine.SwitchToVisualLine();

            Assert.That(engine.CurrentMode, Is.EqualTo(VimModeType.VisualLine));
            Assert.That(modes, Is.EqualTo(new[] { VimModeType.VisualLine }));
        }

        [Test]
        public void SwitchToVisualLineWhenAlreadyDoesNotFire()
        {
            var engine = new VimEngine();
            engine.SwitchToVisualLine();
            var fired = false;
            engine.ModeChanged += _ => fired = true;

            engine.SwitchToVisualLine();

            Assert.That(fired, Is.False);
        }

        [Test]
        public void FullModeCycleTest()
        {
            var engine = new VimEngine();
            var modes = new List<VimModeType>();
            engine.ModeChanged += m => modes.Add(m);

            engine.SwitchToNormal();
            engine.SwitchToVisual();
            engine.SwitchToNormal();
            engine.SwitchToInsert();

            Assert.That(engine.CurrentMode, Is.EqualTo(VimModeType.Insert));
            Assert.That(modes, Is.EqualTo(new[] {
                VimModeType.Normal, VimModeType.Visual, VimModeType.Normal, VimModeType.Insert
            }));
        }

        [Test]
        public void VisualLineCycleTest()
        {
            var engine = new VimEngine();
            var modes = new List<VimModeType>();
            engine.ModeChanged += m => modes.Add(m);

            engine.SwitchToNormal();
            engine.SwitchToVisualLine();
            engine.SwitchToVisual();
            engine.SwitchToVisualLine();
            engine.SwitchToNormal();
            engine.SwitchToInsert();

            Assert.That(engine.CurrentMode, Is.EqualTo(VimModeType.Insert));
            Assert.That(modes, Is.EqualTo(new[] {
                VimModeType.Normal, VimModeType.VisualLine, VimModeType.Visual,
                VimModeType.VisualLine, VimModeType.Normal, VimModeType.Insert
            }));
        }

        [Test]
        public void NoSubscriberDoesNotThrow()
        {
            var engine = new VimEngine();
            Assert.DoesNotThrow(() => engine.SwitchToNormal());
            Assert.DoesNotThrow(() => engine.SwitchToVisual());
            Assert.DoesNotThrow(() => engine.SwitchToVisualLine());
            Assert.DoesNotThrow(() => engine.SwitchToInsert());
        }
    }
}
