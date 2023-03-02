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

    public WindowMovedOrResizedHandler(
      Bus bus,
      WindowService windowService,
      MonitorService monitorService,
      ILogger<WindowMovedOrResizedHandler> logger,
      ContainerService containerService,
      UserConfigService userConfigService,
      KeybindingService keybindingService)
    {
      _bus = bus;
      _windowService = windowService;
      _monitorService = monitorService;
      _logger = logger;
      _containerService = containerService;
      _userConfigService = userConfigService;
      _keyBindingService = keybindingService;
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
    private int findIndex(Point cursorPos)
    {
      int index = 0;

      return index;
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
    private Container findTargetDescendant(Point cursorPos, Container window, out int newIndex)
    {
      newIndex = 0;
      var workspace = WorkspaceService.GetWorkspaceFromChildContainer(window);
      var containers = workspace.SelfAndDescendants.Where(c => c is TilingWindow);
      Container bestCandidate = workspace;
      var innerGap = _userConfigService.GapsConfig.InnerGap;
      var outerGap = _userConfigService.GapsConfig.OuterGap;
      int halfInnerGap = (int)(innerGap / 2) + 4;
      int halfOuterGap = (int)(outerGap / 2) + 1;
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

      //???????????????????
      //when placing in gap exepcion
      var r = bestCandidate.ToRect();
      r = resize(r, -halfInnerGap, -halfInnerGap, halfInnerGap, halfInnerGap);
      if (bestCandidate is Workspace)
      {
        r = resize(r, -halfOuterGap, -halfOuterGap, halfOuterGap, halfOuterGap);
      }


      var critTop = resize(r, 0, 0, 0, -0.9 * r.Height);
      var critBot = resize(r, 0, 0.9 * r.Height, 0, 0);
      var critLeft = resize(r, 0, 0.1 * r.Height, -0.9 * r.Width, -0.1 * r.Height);
      var critRight = resize(r, 0.9 * r.Width, -0.1 * r.Height, 0, -0.1 * r.Height);
      // var critTop = resizeByMultiplying(r, 1, 1, 1, 0.1);
      // var critBot=resizeByMultiplying(r,1,0.1,)

      var topZone = Rect.FromLTRB(r.X, r.Top, r.Right, (int)(r.Top + 0.33 * r.Height));
      var bottomZone = Rect.FromLTRB(r.X, (int)(r.Bottom - 0.33 * r.Height), r.Right, r.Bottom);
      var leftZone = Rect.FromLTRB(r.Left, topZone.Bottom, (int)(r.Left + 0.5 * r.Width), bottomZone.Top);
      var rightZone = Rect.FromLTRB((int)(r.Left + 0.5 * r.Width), topZone.Bottom, r.Right, bottomZone.Top);

      var containerToAdjust = bestCandidate;

      if (bestCandidate is Workspace)
      {
        if (pointIsWithinRect(cursorPos, topZone))
        {
          // _bus.Invoke(new ChangeContainerLayoutCommand(containerToAdjust, Layout.Vertical));
          newIndex = 0;
        }

        if (pointIsWithinRect(cursorPos, bottomZone))
        {
          // _bus.Invoke(new ChangeContainerLayoutCommand(containerToAdjust, Layout.Vertical));
          newIndex = workspace.Children.Count;
        }

        if (pointIsWithinRect(cursorPos, leftZone))
        {
          // _bus.Invoke(new ChangeContainerLayoutCommand(containerToAdjust, Layout.Horizontal));
          newIndex = 0;
        }

        if (pointIsWithinRect(cursorPos, rightZone))
        {
          // _bus.Invoke(new ChangeContainerLayoutCommand(containerToAdjust, Layout.Horizontal));
          newIndex = workspace.Children.Count;
        }
        return bestCandidate.Children[0];

      }

      if (pointIsWithinRect(cursorPos, critTop))
      {

      }
      if (pointIsWithinRect(cursorPos, critBot))
      {

      }
      if (pointIsWithinRect(cursorPos, critLeft))
      {
        containerToAdjust = bestCandidate.Parent;
        // _bus.Invoke(new ChangeContainerLayoutCommand(containerToAdjust, Layout.Horizontal));
        newIndex = bestCandidate.Parent.Index;
        if (bestCandidate.Parent is Workspace)
          return bestCandidate;
        if (bestCandidate.Parent.Parent is Workspace)
          return bestCandidate.Parent;
        return bestCandidate.Parent.Parent;
      }
      if (pointIsWithinRect(cursorPos, critRight))
      {
        containerToAdjust = bestCandidate.Parent;
        // _bus.Invoke(new ChangeContainerLayoutCommand(containerToAdjust, Layout.Horizontal));
        newIndex = bestCandidate.Parent.Index + 1;
        if (bestCandidate.Parent is Workspace)
          return bestCandidate;
        if (bestCandidate.Parent.Parent is Workspace)
          return bestCandidate.Parent;
        return bestCandidate.Parent.Parent;
      }

      if (pointIsWithinRect(cursorPos, topZone))
      {
        _bus.Invoke(new ChangeContainerLayoutCommand(containerToAdjust, Layout.Vertical));
        newIndex = bestCandidate.Index;
      }

      if (pointIsWithinRect(cursorPos, bottomZone))
      {
        _bus.Invoke(new ChangeContainerLayoutCommand(containerToAdjust, Layout.Vertical));
        newIndex = bestCandidate.Index + 1;
      }

      if (pointIsWithinRect(cursorPos, leftZone))
      {
        _bus.Invoke(new ChangeContainerLayoutCommand(containerToAdjust, Layout.Horizontal));
        newIndex = bestCandidate.Index;
      }

      if (pointIsWithinRect(cursorPos, rightZone))
      {
        _bus.Invoke(new ChangeContainerLayoutCommand(containerToAdjust, Layout.Horizontal));
        newIndex = bestCandidate.Index + 1;
      }

      return bestCandidate;

    }

    private void UpdateTilingWindow(TilingWindow window)
    {
      // // Snap window to its original position even if it's not being resized.
      // var hasNoResizableSiblings = window.Parent is Workspace
      //   && !window.SiblingsOfType<IResizable>().Any();

      // if (hasNoResizableSiblings)
      // {
      //   _containerService.ContainersToRedraw.Add(window);
      //   _bus.Invoke(new RedrawContainersCommand());
      //   return;
      // }

      // Remove invisible borders from current placement to be able to compare window width/height.
      var currentPlacement = WindowService.GetPlacementOfHandle(window.Handle).NormalPosition;
      var adjustedPlacement = new Rect
      {
        Left = currentPlacement.Left + window.BorderDelta.Left,
        Right = currentPlacement.Right - window.BorderDelta.Right,
        Top = currentPlacement.Top + window.BorderDelta.Top,
        Bottom = currentPlacement.Bottom - window.BorderDelta.Bottom,
      };

      var deltaWidth = adjustedPlacement.Width - window.Width;
      var deltaHeight = adjustedPlacement.Height - window.Height;
      //NEW THING--------------------------------------------------------
      // Check if window was only moved
      // dont use deltaWidht/height to check, as sometimes windows are weird sizes
      // x, y works better (just not when there is a pixel perfect move on one axis)
      if (window.X != adjustedPlacement.X &&
      window.Y != adjustedPlacement.Y &&
      window.X + window.Width != adjustedPlacement.Right &&
      window.Y + window.Height != adjustedPlacement.Bottom)
      {
        var cursorPos = new Point
        {
          X = WindowsApiService.GetCursorPosition().X,
          Y = WindowsApiService.GetCursorPosition().Y
        };

        if (WindowsApiService.GetKeyState(System.Windows.Forms.Keys.LMenu) > (-127))
        {
          var tilingWindow = window;
          var targetDescendant = findTargetDescendant(cursorPos, window, out var newIndex);

          _bus.Invoke(
            new MoveContainerWithinTreeCommand(
              tilingWindow,
              targetDescendant.Parent as SplitContainer,
              newIndex,
              true
            ));
          _bus.Invoke(new RedrawContainersCommand());
          return;
        }

        var fPos = window.FloatingPlacement;
        window.FloatingPlacement = Rect.FromLTRB(cursorPos.X - (fPos.Width / 2), cursorPos.Y, cursorPos.X + (fPos.Width / 2), cursorPos.Y + fPos.Height);
        _bus.Invoke(new SetFloatingCommand(window));
        return;
      }

      _bus.Invoke(new ResizeWindowCommand(window, ResizeDimension.Width, $"{deltaWidth}px"));
      _bus.Invoke(new ResizeWindowCommand(window, ResizeDimension.Height, $"{deltaHeight}px"));
    }

    private void UpdateFloatingWindow(FloatingWindow window)
    {
      if (WindowsApiService.GetKeyState(System.Windows.Forms.Keys.LMenu) is (-127) or (-128))
      {
        _logger.LogDebug("KKEYDOWN");

        var cursorPos = new Point
        {
          X = WindowsApiService.GetCursorPosition().X,
          Y = WindowsApiService.GetCursorPosition().Y
        };
        // _bus.Invoke(new SetTilingCommand(window));

        //settiling---------------------------------------------------
        // Keep reference to the window's ancestor workspace prior to detaching.
        var workspace = WorkspaceService.GetWorkspaceFromChildContainer(window);

        var insertionTarget = workspace.LastFocusedDescendantOfType<IResizable>();

        var tilingWindow = new TilingWindow(
          window.Handle,
          window.FloatingPlacement,
          window.BorderDelta,
          0
        );

        // Replace the original window with the created tiling window.
        _bus.Invoke(new ReplaceContainerCommand(tilingWindow, window.Parent, window.Index));
        int newIndex;
        var targetDescendant = findTargetDescendant(cursorPos, window, out newIndex);

        // Insert the created tiling window after the last focused descendant of the workspace.
        if (insertionTarget is null)
          _bus.Invoke(new MoveContainerWithinTreeCommand(tilingWindow, workspace, 0, true));
        else
          // _bus.Invoke(
          //   new MoveContainerWithinTreeCommand(
          //     tilingWindow,
          //     insertionTarget.Parent,
          //     insertionTarget.Index + 1,
          //     true
          //   )
          _bus.Invoke(
            new MoveContainerWithinTreeCommand(
              tilingWindow,
              targetDescendant.Parent as SplitContainer,
              newIndex,
              true
            )
          );
        //settiling---------------------------------------------------

        // _bus.Invoke(new MoveContainerWithinTreeCommand(tilingWindow, targetDescendant.Parent as SplitContainer, newIndex, true));
        _bus.Invoke(new RedrawContainersCommand());



      }
      _logger.LogDebug(WindowsApiService.GetCursorPosition().X.ToString());

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
