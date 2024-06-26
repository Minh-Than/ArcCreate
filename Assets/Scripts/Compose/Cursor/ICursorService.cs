using System;
using ArcCreate.Compose.Navigation;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace ArcCreate.Compose.Cursor
{
    public interface ICursorService
    {
        /// <summary>
        /// Gets or sets a value indicating whether or not to enable the lane cursor.
        /// Lane cursor is automatically enabled when <see cref="RequestTimingSelection"/> is called.
        /// </summary>
        bool EnableLaneCursor { get; set; }

        /// <summary>
        /// Gets a value indicating whether or not the cursor is hitting the lane.
        /// </summary>
        bool IsHittingLane { get; }

        /// <summary>
        /// Gets the current cursor timing.
        /// </summary>
        int CursorTiming { get; }

        /// <summary>
        /// Gets the current selected lane.
        /// </summary>
        int CursorLane { get; }

        /// <summary>
        /// Gets the current cursor's world position.
        /// </summary>
        Vector3 CursorWorldPosition { get; }

        /// <summary>
        /// Gets a value indicating whether or not the cursor is on top of gameplay viewport.
        /// </summary>
        bool IsCursorAboveViewport { get; }

        /// <summary>
        /// Request for a selection of a timing value.
        /// </summary>
        /// <param name="confirm">A sub-action for confirming the selection.</param>
        /// <param name="cancel">A sub-action for cancelling the selection.</param>
        /// <param name="update">Callback that's invoked every frame with the current valid value (unless <see cref="constraint"/> is not satisfied).</param>
        /// <param name="constraint">Callback that's invoked to check whether a value is valid or not.</param>
        /// <returns>wasSuccessful: Whether or not confirmed was executed.
        /// <br/>lane: Selected timing value.</returns>
        UniTask<(bool wasSuccessful, int timing)> RequestTimingSelection(
            SubAction confirm,
            SubAction cancel,
            Action<int> update = null,
            Func<int, bool> constraint = null);

        /// <summary>
        /// Request for a selection of a lane on the lane collider.
        /// </summary>
        /// <param name="confirm">A sub-action for confirming the selection.</param>
        /// <param name="cancel">A sub-action for cancelling the selection.</param>
        /// <param name="update">Callback that's invoked every frame with the current valid value (unless <see cref="constraint"/> is not satisfied).</param>
        /// <param name="constraint">Callback that's invoked to check whether a value is valid or not.</param>
        /// <returns>wasSuccessful: Whether or not confirmed was executed.
        /// <br/>lane: Selected lane on the collider.</returns>
        UniTask<(bool wasSuccessful, int lane)> RequestLaneSelection(
            SubAction confirm,
            SubAction cancel,
            Action<int> update = null,
            Func<int, bool> constraint = null);

        /// <summary>
        /// Request for a selection of a point on the vertical collider.
        /// </summary>
        /// <param name="confirm">A sub-action for confirming the selection.</param>
        /// <param name="cancel">A sub-action for cancelling the selection.</param>
        /// <param name="showGridAtTiming">Timing to align the vertical grid to.</param>
        /// <param name="update">Callback that's invoked every frame with the current valid value (unless <see cref="constraint"/> is not satisfied).</param>
        /// <param name="constraint">Callback that's invoked to check whether a value is valid or not.</param>
        /// <returns>wasSuccessful: Whether or not confirmed was executed.
        /// <br/>point: Selected point on the collider.</returns>
        UniTask<(bool wasSuccessful, Vector2 point)> RequestVerticalSelection(
            SubAction confirm,
            SubAction cancel,
            int showGridAtTiming,
            Action<Vector2> update = null,
            Func<Vector2, bool> constraint = null);

        /// <summary>
        /// Forces lane cursor to update even when <see cref="ICursorService.EnableLaneCursor"/> is false.
        /// </summary>
        void ForceUpdateLaneCursor();
    }
}