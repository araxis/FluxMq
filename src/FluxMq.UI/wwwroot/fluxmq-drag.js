const dragThreshold = 5;

let activeDrag = null;

export function startComponentDrag(dotNetRef, componentType, displayName, pointerId, clientX, clientY) {
    finishActiveDrag(false, false);

    const state = {
        dotNetRef,
        componentType,
        displayName,
        pointerId,
        startX: clientX,
        startY: clientY,
        clientX,
        clientY,
        didDrag: false,
        frameRequested: false
    };

    state.onMove = event => {
        if (event.pointerId !== state.pointerId) {
            return;
        }

        state.clientX = event.clientX;
        state.clientY = event.clientY;

        const dx = state.clientX - state.startX;
        const dy = state.clientY - state.startY;
        if (!state.didDrag && Math.hypot(dx, dy) >= dragThreshold) {
            state.didDrag = true;
            document.body.classList.add("flux-component-drag-active");
        }

        if (state.didDrag) {
            event.preventDefault();
            scheduleDragMove(state);
        }
    };

    state.onUp = event => {
        if (event.pointerId !== state.pointerId) {
            return;
        }

        state.clientX = event.clientX;
        state.clientY = event.clientY;
        event.preventDefault();
        finishActiveDrag(state.didDrag, isOverDesigner(state.clientX, state.clientY));
    };

    state.onCancel = event => {
        if (event.pointerId !== state.pointerId) {
            return;
        }

        cancelActiveDrag();
    };

    activeDrag = state;
    window.addEventListener("pointermove", state.onMove, { passive: false });
    window.addEventListener("pointerup", state.onUp, { passive: false });
    window.addEventListener("pointercancel", state.onCancel, { passive: false });
}

function scheduleDragMove(state) {
    if (state.frameRequested) {
        return;
    }

    state.frameRequested = true;
    window.requestAnimationFrame(() => {
        state.frameRequested = false;

        if (activeDrag !== state || !state.didDrag) {
            return;
        }

        state.dotNetRef.invokeMethodAsync(
            "OnComponentDragMove",
            state.componentType,
            state.displayName,
            state.clientX,
            state.clientY,
            isOverDesigner(state.clientX, state.clientY));
    });
}

function finishActiveDrag(didDrag, isDesignerDrop) {
    if (!activeDrag) {
        return;
    }

    const state = activeDrag;
    activeDrag = null;
    removeListeners(state);
    document.body.classList.remove("flux-component-drag-active");

    state.dotNetRef.invokeMethodAsync(
        "OnComponentDragEnd",
        state.componentType,
        state.displayName,
        state.clientX,
        state.clientY,
        didDrag,
        isDesignerDrop);
}

function cancelActiveDrag() {
    if (!activeDrag) {
        return;
    }

    const state = activeDrag;
    activeDrag = null;
    removeListeners(state);
    document.body.classList.remove("flux-component-drag-active");
    state.dotNetRef.invokeMethodAsync("OnComponentDragCancel");
}

function removeListeners(state) {
    window.removeEventListener("pointermove", state.onMove);
    window.removeEventListener("pointerup", state.onUp);
    window.removeEventListener("pointercancel", state.onCancel);
}

function isOverDesigner(clientX, clientY) {
    return document
        .elementsFromPoint(clientX, clientY)
        .some(element => element.closest?.(".flow-designer-root"));
}
