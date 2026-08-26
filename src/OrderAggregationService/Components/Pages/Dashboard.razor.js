// Reports the panel geometry to the component so the number of table rows per page
// can be computed from the viewport instead of being a fixed constant.
//
// Driven by the window resize event rather than a ResizeObserver: the panel's height
// derives from the viewport alone, and resize events fire even when the page is not
// compositing frames (background tab, non-displayed pane), where observer callbacks
// are suppressed.

const registrations = new Map();

export function observe(dotnet, panel) {
    let timer = null;

    const measure = () => {
        // Below the stacked-layout breakpoint the panel is not height-bounded, so
        // auto-sizing would feed back into itself; the component falls back to a
        // fixed page size there.
        const bounded = !window.matchMedia("(max-width: 820px)").matches;
        const row = panel.querySelector("tbody tr");
        const chrome = ["thead", "[class*='panel-head']", "[class*='pager']"]
            .map(selector => panel.querySelector(selector))
            .reduce((sum, el) => sum + (el ? el.offsetHeight : 0), 0);

        dotnet.invokeMethodAsync(
            "OnViewportResized",
            bounded ? panel.clientHeight : 0,
            row ? row.offsetHeight : 0,
            chrome);
    };

    const onResize = () => {
        clearTimeout(timer);
        timer = setTimeout(measure, 100);
    };

    window.addEventListener("resize", onResize);
    registrations.set(panel, { onResize, cancel: () => clearTimeout(timer) });
    measure();
}

export function unobserve(panel) {
    const registration = registrations.get(panel);

    if (registration) {
        window.removeEventListener("resize", registration.onResize);
        // A resize may have queued a debounced measure that has not fired yet. Left
        // alone it would call into the component after its .NET reference is disposed,
        // which surfaces as an error in the browser console on every unlucky navigation.
        registration.cancel();
        registrations.delete(panel);
    }
}
