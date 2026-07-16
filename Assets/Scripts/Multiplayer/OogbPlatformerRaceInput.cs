using System;
using System.Collections.Generic;
using UnityEngine;

namespace Platformer.Multiplayer
{
    public readonly struct OogbPlatformerRaceInput
    {
        private const byte JumpPressedFlag = 1;
        private const byte JumpReleasedFlag = 2;
        private const byte FinishedFlag = 4;
        private const byte SnapshotFlag = 8;
        private const byte ControlEnabledFlag = 16;
        private const byte FacingLeftFlag = 32;
        private const int SnapshotPayloadLength = 20;

        public OogbPlatformerRaceInput(float horizontal, bool jumpPressed, bool jumpReleased, bool finished)
            : this(horizontal, jumpPressed, jumpReleased, finished, false, Vector2.zero, Vector2.zero, 0, true, false)
        {
        }

        public OogbPlatformerRaceInput(
            float horizontal,
            bool jumpPressed,
            bool jumpReleased,
            bool finished,
            bool hasSnapshot,
            Vector2 position,
            Vector2 velocity,
            int jumpState,
            bool controlEnabled,
            bool facingLeft)
        {
            Horizontal = Math.Max(-1f, Math.Min(1f, horizontal));
            JumpPressed = jumpPressed;
            JumpReleased = jumpReleased;
            Finished = finished;
            HasSnapshot = hasSnapshot;
            Position = position;
            Velocity = velocity;
            JumpState = jumpState;
            ControlEnabled = controlEnabled;
            FacingLeft = facingLeft;
        }

        public float Horizontal { get; }
        public bool JumpPressed { get; }
        public bool JumpReleased { get; }
        public bool Finished { get; }
        public bool HasSnapshot { get; }
        public Vector2 Position { get; }
        public Vector2 Velocity { get; }
        public int JumpState { get; }
        public bool ControlEnabled { get; }
        public bool FacingLeft { get; }

        public byte[] Encode()
        {
            var quantizedHorizontal = (sbyte)Math.Round(Horizontal * 100f);
            var flags = (byte)0;

            if (JumpPressed)
                flags |= JumpPressedFlag;

            if (JumpReleased)
                flags |= JumpReleasedFlag;

            if (Finished)
                flags |= FinishedFlag;

            if (HasSnapshot)
                flags |= SnapshotFlag;

            if (ControlEnabled)
                flags |= ControlEnabledFlag;

            if (FacingLeft)
                flags |= FacingLeftFlag;

            if (!HasSnapshot)
                return new[] { unchecked((byte)quantizedHorizontal), flags };

            var bytes = new List<byte>(SnapshotPayloadLength)
            {
                unchecked((byte)quantizedHorizontal),
                flags
            };
            bytes.AddRange(BitConverter.GetBytes(Position.x));
            bytes.AddRange(BitConverter.GetBytes(Position.y));
            bytes.AddRange(BitConverter.GetBytes(Velocity.x));
            bytes.AddRange(BitConverter.GetBytes(Velocity.y));
            bytes.Add((byte)Math.Max(0, Math.Min(255, JumpState)));
            bytes.Add(FacingLeft ? (byte)1 : (byte)0);
            return bytes.ToArray();
        }

        public static bool TryDecode(byte[] payload, out OogbPlatformerRaceInput input)
        {
            input = default;

            if (payload == null || payload.Length < 2)
                return false;

            var horizontal = (sbyte)payload[0] / 100f;
            var flags = payload[1];
            var hasSnapshot = (flags & SnapshotFlag) != 0;

            if (!hasSnapshot)
            {
                input = new OogbPlatformerRaceInput(
                    horizontal,
                    (flags & JumpPressedFlag) != 0,
                    (flags & JumpReleasedFlag) != 0,
                    (flags & FinishedFlag) != 0);
                return true;
            }

            if (payload.Length < SnapshotPayloadLength)
                return false;

            input = new OogbPlatformerRaceInput(
                horizontal,
                (flags & JumpPressedFlag) != 0,
                (flags & JumpReleasedFlag) != 0,
                (flags & FinishedFlag) != 0,
                true,
                new Vector2(BitConverter.ToSingle(payload, 2), BitConverter.ToSingle(payload, 6)),
                new Vector2(BitConverter.ToSingle(payload, 10), BitConverter.ToSingle(payload, 14)),
                payload[18],
                (flags & ControlEnabledFlag) != 0,
                payload.Length > 19 ? payload[19] != 0 : (flags & FacingLeftFlag) != 0);
            return true;
        }
    }
}
