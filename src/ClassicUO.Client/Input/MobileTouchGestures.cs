// SPDX-License-Identifier: BSD-2-Clause

using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace ClassicUO.Input;

/// <summary>Arbitrates a single touch pointer and world pinches in window points.</summary>
internal sealed class MobileTouchGestures
{
    internal const float DragThreshold = 8f;
    private readonly Dictionary<(ulong Device, ulong Finger), Vector2> _fingers = new();
    private (ulong Device, ulong Finger) _primary, _secondary;
    private Vector2 _start;
    private bool _pointer, _dragging, _allowPinch, _pinching;
    private float _pinchDistance;

    internal int FingerCount => _fingers.Count;
    internal bool HasPointer => _pointer;
    internal event Action<Vector2> PointerDown, PointerMove, PointerUp;
    internal event Action PointerCancel, PinchStarted;
    internal event Action<float> PinchScale;

    internal void Down(ulong device, ulong finger, Vector2 position, bool allowPinch)
    {
        var id = (device, finger);
        if (!_fingers.TryAdd(id, position)) return;

        if (_fingers.Count == 1)
        {
            _primary = id;
            _start = position;
            _pointer = true;
            _dragging = false;
            _allowPinch = allowPinch;
            PointerDown?.Invoke(position);
        }
        else if (_fingers.Count == 2 && _pointer && _allowPinch)
        {
            // Never turn either end of a pinch into a click, target or item drop.
            _pointer = false;
            PointerCancel?.Invoke();
            _secondary = id;
            _pinching = allowPinch;
            _pinchDistance = Vector2.Distance(_fingers[_primary], position);
            if (_pinching) PinchStarted?.Invoke();
        }
        else if (_fingers.Count > 2)
        {
            _pinching = false;
        }
    }

    internal void Move(ulong device, ulong finger, Vector2 position)
    {
        var id = (device, finger);
        if (!_fingers.ContainsKey(id)) return;
        _fingers[id] = position;

        if (_pinching)
        {
            float distance = Vector2.Distance(_fingers[_primary], _fingers[_secondary]);
            // A nearly coincident pair has no useful scale baseline. Rebase once
            // separated, then use an absolute ratio so event frequency cannot drift zoom.
            if (_pinchDistance < DragThreshold)
                _pinchDistance = distance;
            else if (distance >= DragThreshold)
                PinchScale?.Invoke(distance / _pinchDistance);
        }
        else if (_pointer && id == _primary)
        {
            _dragging |= Vector2.DistanceSquared(_start, position) >= DragThreshold * DragThreshold;
            if (_dragging) PointerMove?.Invoke(position);
        }
    }

    internal void Up(ulong device, ulong finger, Vector2 position, bool canceled = false)
    {
        var id = (device, finger);
        if (!_fingers.ContainsKey(id)) return;

        if (_pointer && id == _primary)
        {
            if (canceled)
                PointerCancel?.Invoke();
            else
            {
                Move(device, finger, position);
                PointerUp?.Invoke(_dragging ? position : _start);
            }
            _pointer = false;
        }
        // Once either pinch finger lifts, suppress the remainder of this sequence.
        _pinching = false;
        _fingers.Remove(id);
        if (_fingers.Count == 0) Reset();
    }

    internal void Reset()
    {
        if (_pointer) PointerCancel?.Invoke();
        _pointer = _pinching = _allowPinch = _dragging = false;
        _fingers.Clear();
    }
}
