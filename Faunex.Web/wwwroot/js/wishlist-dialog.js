const callbacks = new WeakMap();
export function open(dialog, reference) {
    dispose(dialog);
    const closed = () => reference.invokeMethodAsync('Closed').catch(() => {});
    const cancelled = event => { if (dialog.dataset.busy === 'true') event.preventDefault(); };
    callbacks.set(dialog, { closed, cancelled });
    dialog.addEventListener('close', closed);
    dialog.addEventListener('cancel', cancelled);
    if (!dialog.open) dialog.showModal();
}
export function close(dialog) { if (dialog.open) dialog.close(); }
export function dispose(dialog) {
    const handlers = callbacks.get(dialog);
    if (handlers) { dialog.removeEventListener('close', handlers.closed); dialog.removeEventListener('cancel', handlers.cancelled); }
    callbacks.delete(dialog);
    if (dialog.open) dialog.close();
}
