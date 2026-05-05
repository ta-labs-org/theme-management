window.printPage = function () {
    window.print();
};

window.agentPage = {
    _handlers: {},
    registerEnterHandler: function (dotNetRef, handlerId) {
        const handler = function (e) {
            if (e.key === 'Enter' && !e.shiftKey && e.target.closest('[data-agent-input]')) {
                e.preventDefault();
                dotNetRef.invokeMethodAsync('SendMessageFromJs');
            }
        };
        window.agentPage._handlers[handlerId] = handler;
        document.addEventListener('keydown', handler, true);
    },
    unregisterEnterHandler: function (handlerId) {
        const handler = window.agentPage._handlers[handlerId];
        if (handler) {
            document.removeEventListener('keydown', handler, true);
            delete window.agentPage._handlers[handlerId];
        }
    }
};
