const attachments = new WeakMap();

export function attachWorkspaceResizer(root, dotNetReference) {
  detachWorkspaceResizer(root);

  const handlers = [];
  const handles = root.querySelectorAll("[data-workspace-resize]");

  for (const handle of handles) {
    const onPointerDown = (event) => {
      const side = handle.dataset.workspaceResize;
      if (side !== "left" && side !== "right") {
        return;
      }

      event.preventDefault();
      handle.setPointerCapture?.(event.pointerId);

      const startX = event.clientX;
      const computed = getComputedStyle(root);
      const leftStart = readWidth(root, computed, "--workspace-left-width", ".workspace-left");
      const rightStart = readWidth(root, computed, "--workspace-right-width", ".workspace-right");
      const limits = readLimits(root);
      let committedWidth = side === "left" ? leftStart : rightStart;

      root.classList.add("workspace-resizing");

      const onPointerMove = (moveEvent) => {
        const delta = moveEvent.clientX - startX;
        committedWidth = side === "left"
          ? clamp(leftStart + delta, limits.leftMin, limits.leftMax)
          : clamp(rightStart - delta, limits.rightMin, limits.rightMax);

        root.style.setProperty(
          side === "left" ? "--workspace-left-width" : "--workspace-right-width",
          `${Math.round(committedWidth)}px`);
      };

      const stop = async () => {
        window.removeEventListener("pointermove", onPointerMove);
        window.removeEventListener("pointerup", stop);
        window.removeEventListener("pointercancel", stop);
        root.classList.remove("workspace-resizing");
        try {
          handle.releasePointerCapture?.(event.pointerId);
          await dotNetReference.invokeMethodAsync("CommitPanelWidth", side, committedWidth);
        } catch (error) {
          console.debug("Workspace resize commit failed.", error);
        }
      };

      window.addEventListener("pointermove", onPointerMove);
      window.addEventListener("pointerup", stop);
      window.addEventListener("pointercancel", stop);
    };

    handle.addEventListener("pointerdown", onPointerDown);
    handlers.push(() => handle.removeEventListener("pointerdown", onPointerDown));
  }

  attachments.set(root, handlers);
}

export function detachWorkspaceResizer(root) {
  const handlers = attachments.get(root);
  if (!handlers) {
    return;
  }

  for (const cleanup of handlers) {
    cleanup();
  }

  attachments.delete(root);
}

function readWidth(root, computed, variableName, selector) {
  const fromVariable = Number.parseFloat(computed.getPropertyValue(variableName));
  if (Number.isFinite(fromVariable) && fromVariable > 0) {
    return fromVariable;
  }

  const element = root.querySelector(selector);
  return element?.getBoundingClientRect().width ?? 0;
}

function readLimits(root) {
  return {
    leftMin: readNumber(root.dataset.leftMin, 280),
    leftMax: readNumber(root.dataset.leftMax, 520),
    rightMin: readNumber(root.dataset.rightMin, 320),
    rightMax: readNumber(root.dataset.rightMax, 600)
  };
}

function readNumber(value, fallback) {
  const parsed = Number.parseFloat(value);
  return Number.isFinite(parsed) ? parsed : fallback;
}

function clamp(value, min, max) {
  return Math.min(Math.max(value, min), max);
}
