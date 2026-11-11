using UnityEngine;

namespace HunterSystem.Game
{
    /// <summary>
    /// Tints a renderer through a property block (no material copies). Views call
    /// <see cref="Flash"/> on an event and <see cref="Apply"/> once per frame.
    /// </summary>
    public sealed class BodyTint
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        // Created on first use: Unity forbids making property blocks while a MonoBehaviour is being
        // constructed, and views hold a BodyTint in a field initializer.
        private MaterialPropertyBlock _props;
        private float _flashUntil;
        private Color _flashColor;

        /// <summary>Shows <paramref name="color"/> for <paramref name="seconds"/>, overriding any steady tint.</summary>
        public void Flash(Color color, float seconds)
        {
            _flashColor = color;
            _flashUntil = Time.time + seconds;
        }

        /// <summary>Applies the active flash, else <paramref name="steady"/> if given, else the material's own colour.</summary>
        public void Apply(Renderer renderer, Color? steady = null)
        {
            _props ??= new MaterialPropertyBlock();
            if (Time.time < _flashUntil) _props.SetColor(BaseColorId, _flashColor);
            else if (steady.HasValue) _props.SetColor(BaseColorId, steady.Value);
            else _props.Clear();
            renderer.SetPropertyBlock(_props);
        }
    }
}
