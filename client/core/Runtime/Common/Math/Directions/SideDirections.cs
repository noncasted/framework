﻿using System;
using UnityEngine;

namespace Internal
{
    public enum Direction2
    {
        Forward,
        Backward,
    }

    public enum Direction4
    {
        Up,
        Right,
        Down,
        Left
    }

    // Clockwise from Up, as the 8-way directions of the projects.
    public enum Direction8
    {
        Up = 0,
        Up_Right = 1,
        Right = 2,
        Down_Right = 3,
        Down = 4,
        Down_Left = 5,
        Left = 6,
        Up_Left = 7
    }

    public static class SideExtensions
    {
        // The dominant axis gives the direction; an exact diagonal is horizontal.
        public static Direction4 ToDirection4(this Vector2 vector)
        {
            ThrowIfZero(vector);

            if (Mathf.Abs(vector.x) >= Mathf.Abs(vector.y))
                return vector.x > 0f ? Direction4.Right : Direction4.Left;

            return vector.y > 0f ? Direction4.Up : Direction4.Down;
        }

        // The nearest of the eight directions: sectors of 45 degrees around each one.
        public static Direction8 ToDirection8(this Vector2 vector)
        {
            ThrowIfZero(vector);

            // The angle clockwise from Up, so the sector index is the value of the enum.
            var angle = Mathf.Atan2(vector.x, vector.y) * Mathf.Rad2Deg;
            var sector = Mathf.RoundToInt(Mathf.Repeat(angle, 360f) / 45f) % 8;

            return (Direction8)sector;
        }

        private static void ThrowIfZero(Vector2 vector)
        {
            if (vector == Vector2.zero)
                throw new ArgumentException("A zero vector has no direction", nameof(vector));
        }
    }
}
