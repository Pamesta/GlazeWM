using System.Linq;
using GlazeWM.Domain.Common.Enums;
using GlazeWM.Domain.Common.Utils;
using GlazeWM.Domain.Containers;
using GlazeWM.Domain.Containers.Commands;
using GlazeWM.Domain.Containers.Events;
using GlazeWM.Domain.Monitors;
using GlazeWM.Domain.Windows.Commands;
using GlazeWM.Domain.Workspaces;
using GlazeWM.Infrastructure.Bussing;
using GlazeWM.Infrastructure.Common.Events;
using GlazeWM.Infrastructure.WindowsApi;
using Microsoft.Extensions.Logging;
using System.Windows.Input;
using GlazeWM.Domain.UserConfigs;
using System;

namespace GlazeWM.Domain.Windows.EventHandlers
{
  internal sealed class WindowMovedOrResizedHandler : IEventHandler<WindowMovedOrResizedEvent>
  {
    private readonly Bus _bus;
    private readonly WindowService _windowService;
    private readonly MonitorService _monitorService;
    private readonly ContainerService _containerService;
    private readonly ILogger<WindowMovedOrResizedHandler> _logger;
    private readonly UserConfigService _userConfigService;
    private readonly KeybindingService _keyBindingService;
    private readonly WorkspaceService _workspaceService;

    public WindowMovedOrResizedHandler(
      Bus bus,
      WindowService windowService,
      MonitorService monitorService,
      ILogger<WindowMovedOrResizedHandler> logger,
      ContainerService containerService,
      UserConfigService userConfigService,
      KeybindingService keybindingService,
      WorkspaceService workspaceService)
    {
      _bus = bus;
      _windowService = windowService;
      _monitorService = monitorService;
      _logger = logger;
      _containerService = containerService;
      _userConfigService = userConfigService;
      _keyBindingService = keybindingService;
      _workspaceService = workspaceService;
    }

    public void Handle(WindowMovedOrResizedEvent @event)
    {
      var window = _windowService.GetWindows()
        .FirstOrDefault(window => window.Handle == @event.WindowHandle);

      if (window is null)
        return;

      _logger.LogWindowEvent("Window moved/resized", window);

      if (window is TilingWindow)
      {
        UpdateTilingWindow(window as TilingWindow);
        return;
      }

      if (window is FloatingWindow)
        UpdateFloatingWindow(window as FloatingWindow);
    }
    private bool pointIsWithinRect(Point point, Rect rect)
    {
      if ((point.X >= rect.Width + rect.X) ||
      (point.X < rect.X) ||
      (point.Y < rect.Y) ||
      (point.Y >= rect.Height + rect.Y))
      {
        return false;

      }
      return true;
    }
    private Rect resize(Rect rect, double left, double top, double right, double bottom)
    {
      return Rect.FromLTRB(rect.Left + (int)left, rect.Top + (int)top, rect.Right + (int)right, rect.Bottom + (int)bottom);
    }
    private Rect resizeByMultiplying(Rect rect, double left, double top, double right, double bottom)
    {
      return Rect.FromLTRB((int)(rect.Left * left), (int)(rect.Top * top), (int)(rect.Right * right), (int)(rect.Bottom * bottom));
    }
    private enum Zone
    {
      Left,
      LeftCritical,
      Top,
      TopCritical,
      Right,
      RigtCritical,
      Bottom,
      BottomCritical
    }
    private Container ClosestContainerToPoint(Point cursorPos)
    {
      var activeWorkspaces = _workspaceService.GetActiveWorkspaces();
      Container bestCandidate = null;
      int minX = 99999;
      int minY = 99999;
      foreach (var workspace in activeWorkspaces)
      {
        var containers = workspace.SelfAndDescendants.Where(c => c is TilingWindow);

        foreach (var c in containers)
        {
          int distanceFromLeft = Math.Abs(cursorPos.X - c.ToRect().Left);
          int distanceFromRight = Math.Abs(cursorPos.X - c.ToRect().Right);
          int distanceFromTop = Math.Abs(cursorPos.Y - c.ToRect().Top);
          int distanceFromBottom = Math.Abs(cursorPos.Y - c.ToRect().Bottom);

          int distanceX = Math.Min(distanceFromLeft, distanceFromRight);
          int distanceY = Math.Min(distanceFromTop, distanceFromBottom);

          if (distanceX <= minX && distanceY <= minY)
          {
            bestCandidate = c;
          }
        }
      }

      return bestCandidate;
    }
    private Container ContainerContainingCursor(Point cursorPos, Container window)
    {
      Container bestCandidate = null;
      var monitors = _monitorService.GetMonitors();

      var innerGap = _userConfigService.GapsConfig.InnerGap;
      int halfInnerGap = (int)(innerGap / 2) + 4;

      foreach (var monitor in monitors)
      {
        var workspace = monitor.DisplayedWorkspace;
        var containers = workspace.SelfAndDescendants.Where(c => c is TilingWindow or Workspace);

        foreach (var c in containers)
        {
          if (c.Id == window.Id)
          {
            continue;
          }
          if (pointIsWithinRect(cursorPos, resize(c.ToRect(), -halfInnerGap, -halfInnerGap, halfInnerGap, halfInnerGap)))
          {
            bestCandidate = c;
          }
        }
      }

      return bestCandidate;
    }

    private Zone ZoneContainingCursor(Point cursorPos, Container container, out bool isCritical)
    {
      isCritical = false;

      var innerGap = _userConfigService.GapsConfig.InnerGap;
      int halfInnerGap = (int)(innerGap / 2) + 4;
      var r = container.ToRect();
      r = resize(r, -halfInnerGap, -halfInnerGap, halfInnerGap, halfInnerGap);

      //TODO: allow customize critical zone from config
      var critTop = resize(r, 0, 0, 0, -0.9 * r.Height);
      var critBot = resize(r, 0, 0.9 * r.Height, 0, 0);
      var critLeft = resize(r, 0, 0.1 * r.Height, -0.9 * r.Width, -0.1 * r.Height);
      var critRight = resize(r, 0.9 * r.Width, -0.1 * r.Height, 0, -0.1 * r.Height);

      var topZone = Rect.FromLTRB(r.X, r.Top, r.Right, (int)(r.Top + (0.33 * r.Height)));
      var bottomZone = Rect.FromLTRB(r.X, (int)(r.Bottom - (0.33 * r.Height)), r.Right, r.Bottom);
      var leftZone = Rect.FromLTRB(r.Left, topZone.Bottom, (int)(r.Left + (0.5 * r.Width)), bottomZone.Top);
      var rightZone = Rect.FromLTRB((int)(r.Left + (0.5 * r.Width)), topZone.Bottom, r.Right, bottomZone.Top);
      if (pointIsWithinRect(cursorPos, critTop) || pointIsWithinRect(cursorPos, critBot) || pointIsWithinRect(cursorPos, critLeft) || pointIsWithinRect(cursorPos, critRight))
      {
        isCritical = true;
      }

      if (pointIsWithinRect(cursorPos, topZone))
      {
        return Zone.Top;
      }

      if (pointIsWithinRect(cursorPos, bottomZone))
      {
        return Zone.Bottom;
      }

      if (pointIsWithinRect(cursorPos, leftZone))
      {
        return Zone.Left;
      }

      if (pointIsWithinRect(cursorPos, rightZone))
      {
        return Zone.Right;
      }

      return Zone.Bottom;
    }

    private void MovingLogic(Point cursorPos, TilingWindow window, Container insertionTargetContainer)
    {

      var previousParent = window.Parent;
      var insertInto = insertionTargetContainer;
      var containerToAdjust = insertionTargetContainer;
      var zone = ZoneContainingCursor(cursorPos, insertionTargetContainer, out bool critical);
      _bus.Invoke(new DetachAndResizeContainerCommand(window));
      int addToNewIndex = 0;
      Direction moveDirection = Direction.Down;
      Layout newLayout = Layout.Vertical;

      if (zone == Zone.Top)
      {
        newLayout = Layout.Vertical;
        moveDirection = Direction.Up;
      }
      else if (zone == Zone.Bottom)
      {
        newLayout = Layout.Vertical;
        addToNewIndex = 1;
        moveDirection = Direction.Down;
      }
      else if (zone == Zone.Left)
      {
        newLayout = Layout.Horizontal;
        moveDirection = Direction.Left;
      }
      else if (zone == Zone.Right)
      {
        newLayout = Layout.Horizontal;
        addToNewIndex = 1;
        moveDirection = Direction.Right;
      }

      if (previousParent is SplitContainer && previousParent.Children.Count == 1 && previousParent is not Workspace)
      {
        _bus.Invoke(new FlattenSplitContainerCommand(previousParent as SplitContainer));
      }

      _bus.Invoke(new ChangeContainerLayoutCommand(containerToAdjust, newLayout));

      insertInto = insertionTargetContainer.Parent;
      int newIndex = insertionTargetContainer.Index + addToNewIndex;

      bool hadSiblings = insertionTargetContainer.HasSiblings();
      _bus.Invoke(new AttachAndResizeContainerCommand(window, insertInto, newIndex));

      if (critical && !hadSiblings)
      {
        _bus.Invoke(new MoveWindowCommand(window, moveDirection));
      }
    }

    private void UpdateTilingWindow(TilingWindow window)
    {
      // Remove invisible borders from current placement to be able to compare window width/height.
      var currentPlacement = WindowService.GetPlacementOfHandle(window.Handle).NormalPosition;
      var adjustedPlacement = new Rect
      {
        Left = currentPlacement.Left + window.BorderDelta.Left,
        Right = currentPlacement.Right - window.BorderDelta.Right,
        Top = currentPlacement.Top + window.BorderDelta.Top,
        Bottom = currentPlacement.Bottom - window.BorderDelta.Bottom,
      };

      // Check if window was only moved
      // dont use deltaWidht/height to check, as sometimes windows are weird sizes
      // x, y works better (just not when there is a pixel perfect move on one axis)
      bool windowWasOnlyMoved = window.X != adjustedPlacement.X &&
      window.Y != adjustedPlacement.Y &&
      window.X + window.Width != adjustedPlacement.Right &&
      window.Y + window.Height != adjustedPlacement.Bottom;

      if (windowWasOnlyMoved)
      {
        var cursorPos = new Point
        {
          X = WindowsApiService.GetCursorPosition().X,
          Y = WindowsApiService.GetCursorPosition().Y
        };

        bool ALTisDown = WindowsApiService.GetKeyState(System.Windows.Forms.Keys.LMenu) <= (-127);

        //TODO: invert control option in config
        if (!ALTisDown)
        {
          var insertionTargetContainer = ContainerContainingCursor(cursorPos, window);

          if (insertionTargetContainer is null)
          {
            // Just redraw and do nothing if cursor ir directly on workspace (empty area (i recommend not setting huge outer gaps :D))
            _containerService.ContainersToRedraw.Add(window);
            _bus.Invoke(new RedrawContainersCommand());
            return;
          }

          if (insertionTargetContainer is Workspace)
          {
            if ((insertionTargetContainer as Workspace).ChildrenOfType<TilingWindow>().Count() == 0)
            {
              _bus.Invoke(new MoveContainerWithinTreeCommand(window, insertionTargetContainer as Workspace, 0, true));
            }
            _containerService.ContainersToRedraw.Add(window);
            _bus.Invoke(new RedrawContainersCommand());
            return;
          }

          MovingLogic(cursorPos, window, insertionTargetContainer);

          //TODO: setFocus???????
          _containerService.ContainersToRedraw.Add(insertionTargetContainer.Parent);
          _bus.Invoke(new RedrawContainersCommand());
          return;
        }

        //TODO: this places top of window where cursor is after detaching, change this so something like ALTSNAP could be used to move windows
        var fPos = window.FloatingPlacement;
        window.FloatingPlacement = Rect.FromLTRB(cursorPos.X - (fPos.Width / 2), cursorPos.Y, cursorPos.X + (fPos.Width / 2), cursorPos.Y + fPos.Height);
        // _bus.Invoke(new SetFloatingCommand(window));
        var floatingWindow = new FloatingWindow(
          window.Handle,
          window.FloatingPlacement,
          window.BorderDelta
        );

        _bus.Invoke(new ReplaceContainerCommand(floatingWindow, window.Parent, window.Index));

        // Change floating window's parent workspace if out of its bounds.
        UpdateParentWorkspace(floatingWindow);

        _containerService.ContainersToRedraw.Add(_containerService.ContainerTree);
        _bus.Invoke(new RedrawContainersCommand());
        return;
      }

      var deltaWidth = adjustedPlacement.Width - window.Width;
      var deltaHeight = adjustedPlacement.Height - window.Height;

      _bus.Invoke(new ResizeWindowCommand(window, ResizeDimension.Width, $"{deltaWidth}px"));
      _bus.Invoke(new ResizeWindowCommand(window, ResizeDimension.Height, $"{deltaHeight}px"));
    }

    private void UpdateFloatingWindow(FloatingWindow window)
    {
      bool ALTisDown = WindowsApiService.GetKeyState(System.Windows.Forms.Keys.LMenu) <= (-127);
      if (ALTisDown)
      {
        var cursorPos = new Point
        {
          X = WindowsApiService.GetCursorPosition().X,
          Y = WindowsApiService.GetCursorPosition().Y
        };

        var insertionTargetContainer = ContainerContainingCursor(cursorPos, window);

        if (insertionTargetContainer is null)
        {
          // oops you missed the target, window gets reset
          _containerService.ContainersToRedraw.Add(window);
          _bus.Invoke(new RedrawContainersCommand());
          return;
        }

        if (insertionTargetContainer is Workspace)
        {

          if ((insertionTargetContainer as Workspace).ChildrenOfType<TilingWindow>().Count() == 0)
          {
            var tilingWindow2 = new TilingWindow(
              window.Handle,
              window.FloatingPlacement,
              window.BorderDelta,
              0
            );

            // Replace the original window with the created tiling window.
            _bus.Invoke(new ReplaceContainerCommand(tilingWindow2, window.Parent, window.Index));
            _bus.Invoke(new MoveContainerWithinTreeCommand(tilingWindow2, insertionTargetContainer, 0, true));
          }

          _containerService.ContainersToRedraw.Add(window.Parent);
          _containerService.ContainersToRedraw.Add(insertionTargetContainer);
          _bus.Invoke(new RedrawContainersCommand());
          return;
        }

        //settiling---------------------------------------------------
        var tilingWindow = new TilingWindow(
          window.Handle,
          window.FloatingPlacement,
          window.BorderDelta,
          0
        );

        // Replace the original window with the created tiling window.
        _bus.Invoke(new ReplaceContainerCommand(tilingWindow, window.Parent, window.Index));
        //settiling---------------------------------------------------

        MovingLogic(cursorPos, tilingWindow, insertionTargetContainer);
        UpdateParentWorkspace(tilingWindow);

        //TODO: setFocus???????
        _bus.Invoke(new RedrawContainersCommand());
        return;
      }

      // Update state with new location of the floating window.
      window.FloatingPlacement = WindowService.GetPlacementOfHandle(window.Handle).NormalPosition;

      // Change floating window's parent workspace if out of its bounds.
      UpdateParentWorkspace(window);
    }

    private void UpdateParentWorkspace(Window window)
    {
      var currentWorkspace = WorkspaceService.GetWorkspaceFromChildContainer(window);

      // Get workspace that encompasses most of the window.
      var targetMonitor = _monitorService.GetMonitorFromHandleLocation(window.Handle);
      var targetWorkspace = targetMonitor.DisplayedWorkspace;

      // Ignore if window is still within the bounds of its current workspace.
      if (currentWorkspace == targetWorkspace)
        return;

      // Change the window's parent workspace.
      _bus.Invoke(new MoveContainerWithinTreeCommand(window, targetWorkspace, false));
      _bus.Emit(new FocusChangedEvent(window));
    }
  }
}
