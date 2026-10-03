// Local Unity Editor connection; does not start another Editor instance.
const fs = require('fs');
const WebSocket = require('../Library/PackageCache/com.gamelovers.mcp-unity@382a43a30f4d/Server~/node_modules/ws');
const request = JSON.parse(fs.readFileSync(process.argv[2], 'utf8').replace(/^\uFEFF/, ''));
const token = fs.readFileSync('Library/McpUnity/bridge-token', 'utf8').trim();
const socket = new WebSocket('ws://[::1]:8091/McpUnity', {headers: {
  'X-Client-Name':'Drawing repair',
  'Authorization':'Basic ' + Buffer.from('mcp-unity:' + token).toString('base64')
}});
const timeout = setTimeout(() => { console.error('Unity request timed out'); socket.terminate(); process.exitCode = 1; }, 15000);
socket.on('open', () => socket.send(JSON.stringify({id:'drawing-repair', method:request.method, params:request.params || {}})));
socket.on('message', data => {
  const reply = JSON.parse(data.toString());
  if (reply.id !== 'drawing-repair') return;
  console.log(JSON.stringify(reply));
  clearTimeout(timeout);
  socket.close();
});
socket.on('error', error => { console.error(error.message); clearTimeout(timeout); process.exitCode = 1; });
