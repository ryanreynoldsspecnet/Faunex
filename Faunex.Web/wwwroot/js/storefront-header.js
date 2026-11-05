const connections = new WeakMap();
export function connect(root) {
    if (!root || !root.isConnected) return;
    disconnect(root);
    const controller = new AbortController();
    const signal = controller.signal;
    document.addEventListener('pointerdown', event => {
        root.querySelectorAll('details[open]').forEach(menu => { if (!menu.contains(event.target)) menu.open = false; });
    }, { signal });
    root.addEventListener('keydown', event => {
        if (event.key === 'Escape') root.querySelectorAll('details[open]').forEach(menu => { menu.open = false; menu.querySelector('summary')?.focus(); });
    }, { signal });
    connections.set(root, controller);
}
export function disconnect(root) { connections.get(root)?.abort(); connections.delete(root); }
