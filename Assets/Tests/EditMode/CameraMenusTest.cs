using HunterSystem.Game;
using NUnit.Framework;
using UnityEngine;

namespace HunterSystem.Tests
{
    /// <summary>
    /// Several menus (the arena panel, the stat screen) can hold the cursor at once. The camera
    /// stays in menu mode until the last one closes, so closing one cannot hand control back to
    /// the game behind another.
    /// </summary>
    public class CameraMenusTest
    {
        private GameObject _go;
        private ThirdPersonCamera _camera;

        [SetUp]
        public void CreateCamera()
        {
            _go = new GameObject("Camera");
            _camera = _go.AddComponent<ThirdPersonCamera>();
        }

        [TearDown]
        public void DestroyCamera() => Object.DestroyImmediate(_go);

        [Test]
        public void StaysInMenuModeUntilTheLastMenuCloses()
        {
            object panel = new object(), statScreen = new object();

            _camera.ReleaseCursorForMenu(panel);
            _camera.ReleaseCursorForMenu(statScreen);
            _camera.EndMenu(statScreen, recapture: true);

            Assert.That(_camera.InMenu, Is.True, "the panel is still open");

            _camera.EndMenu(panel, recapture: true);
            Assert.That(_camera.InMenu, Is.False);
        }

        [Test]
        public void ClosingTheSameMenuTwiceDoesNotCloseAnother()
        {
            object panel = new object(), statScreen = new object();
            _camera.ReleaseCursorForMenu(panel);
            _camera.ReleaseCursorForMenu(statScreen);

            _camera.EndMenu(statScreen, recapture: false);
            _camera.EndMenu(statScreen, recapture: false);

            Assert.That(_camera.InMenu, Is.True);
        }
    }
}
