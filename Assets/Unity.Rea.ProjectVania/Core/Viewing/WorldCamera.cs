using System;
using Unity.Rea.ProjectVania.Core.Commands;
using Unity.Rea.ProjectVania.Core.Display;
using Unity.Rea.ProjectVania.Core.Settings;
using Unity.Rea.ProjectVania.Core.World;

namespace Unity.Rea.ProjectVania.Core.Viewing
{
    /// <summary>
    /// The original's <c>CameraManager</c> (RECONSTRUCTION 7), one per player, stepped by the world after the characters
    /// move. Only the world changes it, applying that player's commands. Its arithmetic is the original's, in its order and single precision, with every constant from camera.json:
    /// <list type="bullet">
    /// <item><see cref="Position"/> is the <b>negative</b> of the world point at the screen centre, as the original kept it.</item>
    /// <item>Each step, by mode: Free does nothing, LockPlayer sets the position to the target, LoosePlayer and LooseCursor
    /// pull the velocity toward it by <c>followGain</c> (<c>MoveToLocation</c>); then a velocity under <c>snapBelow</c>
    /// snaps to 0 per axis, the position moves by the velocity, the velocity is damped by <c>damping</c>, and the
    /// centre is clamped to the map (<c>CheckBounds</c>, zeroing the velocity on a clamped axis).</item>
    /// <item>The scroll keys add to the velocity (so scrolling is an acceleration the damping settles), the zoom keys to
    /// <see cref="Scale"/> (unclamped) and the rotate keys to <see cref="Rotation"/>.</item>
    /// <item>The view is the original's matrix, <c>Scale(s^3) * RotationZ(r) * Translation(W/2 + X, H/2 + Y)</c>
    /// (<see cref="ToScreen"/>): it scales and rotates about the world's origin, not the screen centre, as the original did.</item>
    /// </list>
    /// </summary>
    public sealed class WorldCamera
    {
        /// <summary>The smallest magnification <see cref="ViewAt"/> reports, so a zoom to scale 0 (which collapses the
        /// original's view to one point) still has a finite view to draw.</summary>
        public const float MinimumMagnification = 1e-4f;

        readonly CameraData _data;
        readonly double _snapBelow;
        PixelVector _position;
        PixelVector _velocity;

        public WorldCamera(CameraData data)
        {
            _data = data ?? throw new ArgumentNullException(nameof(data));
            // The original compared the float velocity with the double literal 0.01; widening camera.json's float
            // through decimal recovers that literal, so a velocity of exactly 0.01f still snaps as it did.
            _snapBelow = (double)(decimal)data.SnapBelow;
            // CameraManager.Initialize.
            _position = new PixelVector(data.InitialPosition.X, data.InitialPosition.Y);
            _velocity = new PixelVector(0f, 0f);
            Rotation = data.InitialRotation;
            Scale = data.InitialScale;
            Mode = data.InitialMode;
            PreviousPosition = _position;
            PreviousRotation = Rotation;
            PreviousScale = Scale;
        }

        public CameraMode Mode { get; private set; }

        /// <summary>The negative of the world point at the screen centre (when unrotated and unzoomed), in pixels.</summary>
        public PixelVector Position => _position;

        public PixelVector Velocity => _velocity;

        /// <summary>Radians; positive turns the world clockwise on screen.</summary>
        public float Rotation { get; private set; }

        /// <summary>The zoom; the view magnifies by its cube (<see cref="Magnification"/>).</summary>
        public float Scale { get; private set; }

        /// <summary>The position, rotation and scale before the last step, for interpolating the view.</summary>
        public PixelVector PreviousPosition { get; private set; }

        public float PreviousRotation { get; private set; }

        public float PreviousScale { get; private set; }

        /// <summary>The world point the camera centres on while unrotated and unzoomed (<c>-Position</c>).</summary>
        public PixelVector Centre => new PixelVector(-_position.X, -_position.Y);

        /// <summary><c>scale * scale * scale</c>: the factor the original's matrix scales the world by.</summary>
        public float Magnification => Magnify(Scale);

        /// <summary><c>CycleCameraMode</c>: the next mode of camera.json's cycle (Free, LockPlayer, LoosePlayer, LooseCursor), wrapping.</summary>
        internal void CycleMode()
        {
            var cycle = _data.ModeCycle;
            var at = cycle.IndexOf(Mode);
            Mode = cycle[(at + 1) % cycle.Count];
        }

        /// <summary>Sets the mode directly (the editor toggle and Reset did, <c>Camera.Mode = ...</c>).</summary>
        internal void SetMode(CameraMode mode) => Mode = mode;

        /// <summary>One camera key or wheel notch: <c>ScrollLeft</c> ... <c>RotateRight</c> at camera.json's step.</summary>
        internal void Nudge(CameraNudge nudge)
        {
            switch (nudge)
            {
                case CameraNudge.ScrollLeft: Scroll(-1, 0, _data.ScrollStep); break;
                case CameraNudge.ScrollRight: Scroll(1, 0, _data.ScrollStep); break;
                case CameraNudge.ScrollUp: Scroll(0, -1, _data.ScrollStep); break;
                case CameraNudge.ScrollDown: Scroll(0, 1, _data.ScrollStep); break;
                case CameraNudge.ZoomIn: Scale += _data.ZoomStep; break;
                case CameraNudge.ZoomOut: Scale -= _data.ZoomStep; break;
                case CameraNudge.RotateLeft: Rotation -= _data.RotateStep; break;
                case CameraNudge.RotateRight: Rotation += _data.RotateStep; break;
            }
        }

        /// <summary>
        /// Scrolls the view by <paramref name="amount"/> in a screen direction (<paramref name="dx"/>, <paramref name="dy"/>:
        /// -1, 0 or 1, y down). The original's <c>ScrollLeft(vel)</c> added to the velocity, so the view moves left as
        /// <c>Position.X</c> grows; the editor scrolls by camera.json's <c>editorScrollSteps</c>.
        /// </summary>
        internal void Scroll(int dx, int dy, float amount)
        {
            _velocity.X -= Math.Sign(dx) * amount;
            _velocity.Y -= Math.Sign(dy) * amount;
        }

        /// <summary>Remembers the state the step starts from, before its commands, so a zoom or turn interpolates too.</summary>
        internal void BeginStep()
        {
            PreviousPosition = _position;
            PreviousRotation = Rotation;
            PreviousScale = Scale;
        }

        /// <summary>The editor's direction press (<c>Cursor.MoveCellLeft</c> ...): a scroll by camera.json's <c>editorScrollSteps</c>' cell step, 4.</summary>
        internal void ScrollCell(int dx, int dy) => Scroll(dx, dy, _data.EditorScrollSteps.Y);

        /// <summary>
        /// <c>CameraManager.Update</c>: follow <paramref name="target"/> by the mode (none for Free, or when there is no
        /// target), move, damp and clamp to <paramref name="map"/> (no clamp without one).
        /// </summary>
        internal void Step(PixelVector? target, Map map)
        {
            if (target.HasValue)
            {
                switch (Mode)
                {
                    case CameraMode.LockPlayer:
                        _position.X = -target.Value.X;
                        _position.Y = -target.Value.Y;
                        break;
                    case CameraMode.LoosePlayer:
                    case CameraMode.LooseCursor:
                        MoveToLocation(target.Value);
                        break;
                }
            }

            if (Math.Abs((double)_velocity.X) < _snapBelow) _velocity.X = 0f;
            if (Math.Abs((double)_velocity.Y) < _snapBelow) _velocity.Y = 0f;

            _position.X += _velocity.X;
            _position.Y += _velocity.Y;
            _velocity.X *= _data.Damping;
            _velocity.Y *= _data.Damping;

            if (map != null) CheckBounds(map);
        }

        /// <summary>
        /// <c>MoveToLocation</c>: the velocity gains <c>followGain</c> times the gap. The original measured the gap with
        /// absolute values, <c>-(|L| - |Position|)</c>, which is the true gap for the positive-quadrant map it ran on.
        /// </summary>
        void MoveToLocation(PixelVector location)
        {
            var run = -(Math.Abs(location.X) - Math.Abs(_position.X));
            var rise = -(Math.Abs(location.Y) - Math.Abs(_position.Y));
            _velocity.X += _data.FollowGain * run;
            _velocity.Y += _data.FollowGain * rise;
        }

        /// <summary><c>CheckBounds</c>: the centre stays inside (0, 0)-(Width, Height), so half a screen past an edge can show.</summary>
        void CheckBounds(Map map)
        {
            if (-_position.X < 0) { _position.X = 0f; _velocity.X = 0f; }
            if (-_position.Y < 0) { _position.Y = 0f; _velocity.Y = 0f; }
            if (-_position.X > map.Width) { _position.X = -map.Width; _velocity.X = 0f; }
            if (-_position.Y > map.Height) { _position.Y = -map.Height; _velocity.Y = 0f; }
        }

        /// <summary>
        /// Where the original's <c>TransformationMatrix</c> puts world point <paramref name="world"/> on the 800 x 600
        /// screen (pixels, y down), at the current state.
        /// </summary>
        public PixelVector ToScreen(PixelVector world) => ToScreen(world, _position, Rotation, Magnification);

        /// <summary>The view between the previous and the current step, <paramref name="alpha"/> 0..1, for drawing.</summary>
        public CameraView ViewAt(float alpha)
        {
            var position = new PixelVector(Lerp(PreviousPosition.X, _position.X, alpha), Lerp(PreviousPosition.Y, _position.Y, alpha));
            var rotation = Lerp(PreviousRotation, Rotation, alpha);
            var magnification = Magnify(Lerp(PreviousScale, Scale, alpha));
            if (Math.Abs(magnification) < MinimumMagnification)
            {
                magnification = magnification < 0f ? -MinimumMagnification : MinimumMagnification;
            }

            // The world point the matrix puts at the screen centre: k R(r) Q + (W/2, H/2) + P = (W/2, H/2), so
            // Q = R(-r)(-P) / k.
            var cos = (float)Math.Cos(rotation);
            var sin = (float)Math.Sin(rotation);
            var x = -position.X;
            var y = -position.Y;
            var centre = new PixelVector((x * cos + y * sin) / magnification, (-x * sin + y * cos) / magnification);
            return new CameraView(centre, rotation, magnification, position);
        }

        /// <summary>The original's matrix applied to <paramref name="world"/>: scale, then rotate about the origin, then translate.</summary>
        public static PixelVector ToScreen(PixelVector world, PixelVector position, float rotation, float magnification)
        {
            var x = world.X * magnification;
            var y = world.Y * magnification;
            var cos = (float)Math.Cos(rotation);
            var sin = (float)Math.Sin(rotation);
            // XNA's row-vector CreateRotationZ: (x cos - y sin, x sin + y cos).
            return new PixelVector(
                x * cos - y * sin + PixelPerfect.ReferenceWidth / 2f + position.X,
                x * sin + y * cos + PixelPerfect.ReferenceHeight / 2f + position.Y);
        }

        // Exact at both ends, so a whole step draws the stepped state itself.
        static float Lerp(float from, float to, float alpha) =>
            alpha <= 0f ? from : alpha >= 1f ? to : from + (to - from) * alpha;

        float Magnify(float scale)
        {
            var exponent = _data.ScaleExponent;
            if (exponent == Math.Floor(exponent) && exponent <= 16f)
            {
                // The original multiplied it out (scale * scale * scale), which keeps a negative scale's sign.
                var result = 1f;
                for (var i = 0; i < (int)exponent; i++) result *= scale;
                return result;
            }

            return Math.Sign(scale) * (float)Math.Pow(Math.Abs(scale), exponent);
        }
    }

    /// <summary>What a camera shows, for presentation: the world point at the screen centre, the turn and the magnification.</summary>
    public readonly struct CameraView
    {
        public CameraView(PixelVector centre, float rotation, float magnification, PixelVector position)
        {
            Centre = centre;
            Rotation = rotation;
            Magnification = magnification;
            Position = position;
        }

        /// <summary>The world point (pixels, y down) drawn at the screen centre.</summary>
        public PixelVector Centre { get; }

        /// <summary>Radians, positive clockwise on screen.</summary>
        public float Rotation { get; }

        /// <summary>The scale cubed; negative turns the view half way round, as the original's negative scale did.</summary>
        public float Magnification { get; }

        /// <summary>The camera's (interpolated) position, the original's matrix translation less half the screen.</summary>
        public PixelVector Position { get; }

        /// <summary>Where this view puts world point <paramref name="world"/> on the 800 x 600 screen (pixels, y down).</summary>
        public PixelVector ToScreen(PixelVector world) => WorldCamera.ToScreen(world, Position, Rotation, Magnification);
    }
}
