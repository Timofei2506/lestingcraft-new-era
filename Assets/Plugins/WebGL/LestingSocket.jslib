mergeInto(LibraryManager.library, {
    LC_Connect: function(url, name) {
        if (Module.lestingSocket) Module.lestingSocket.close();
        var objectName = UTF8ToString(name);
        var ws = new WebSocket(UTF8ToString(url));
        Module.lestingSocket = ws;
        ws.onopen = function() { SendMessage(objectName, 'OnSocketStatus', '@open'); };
        ws.onmessage = function(e) { SendMessage(objectName, 'OnSocketMessage', e.data); };
        ws.onerror = function() { SendMessage(objectName, 'OnSocketStatus', 'WebSocket connection error'); };
        ws.onclose = function() { SendMessage(objectName, 'OnSocketStatus', '@closed'); };
    },
    LC_Send: function(json) {
        var ws = Module.lestingSocket;
        if (ws && ws.readyState === 1) ws.send(UTF8ToString(json));
    },
    LC_Close: function() {
        var ws = Module.lestingSocket;
        if (ws) { ws.onopen = ws.onmessage = ws.onerror = ws.onclose = null; ws.close(); }
        Module.lestingSocket = null;
    }
});
