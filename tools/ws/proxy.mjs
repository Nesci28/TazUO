import net from 'node:net';
import ws from 'ws';

/**
 * This development gateway forwards browser WebSocket traffic to a shard TCP endpoint. It is not
 * production-ready: deploy authentication, origin checks, rate limits, and TLS termination before
 * exposing it publicly.
 */
const source = process.env.TAZUO_WS_SOURCE ?? '127.0.0.1:2594';
const target = process.env.TAZUO_TCP_TARGET ?? '127.0.0.1:2593';
const packetLog = process.env.TAZUO_PACKET_LOG === '1';
function endpoint(value, name) {
  const match = /^(.+):(\d+)$/.exec(value);
  if (!match) throw new Error(`${name} must use host:port`);
  return { host: match[1], port: Number(match[2]) };
}
const listen = endpoint(source, 'TAZUO_WS_SOURCE');
const upstream = endpoint(target, 'TAZUO_TCP_TARGET');
const { Server: WebSocketServer } = ws;
const server = new WebSocketServer({ host: listen.host, port: listen.port });
server.on('listening', () => console.log(`TazUO development WebSocket gateway: ws://${source} -> tcp://${target}`));
server.on('error', error => { console.error(`WebSocket gateway error: ${error.message}`); process.exitCode = 1; });
server.on('connection', client => {
  const socket = net.createConnection(upstream.port, upstream.host);
  let closed = false;
  const close = () => { if (closed) return; closed = true; socket.destroy(); if (client.readyState < 2) client.close(); };
  socket.on('connect', () => { if (packetLog) console.log('PACKETS: connection established'); });
  socket.on('data', data => {
    if (packetLog) console.log(`PACKETS server->client ${data.length} bytes ${data.subarray(0, 8).toString('hex')}`);
    if (client.readyState === 1) client.send(data, { binary: true });
  });
  socket.on('error', error => { console.error(`TCP upstream error: ${error.message}`); close(); });
  socket.on('close', close);
  client.on('message', data => {
    const bytes = Buffer.from(data);
    if (packetLog) console.log(`PACKETS client->server ${bytes.length} bytes ${bytes.subarray(0, 8).toString('hex')}`);
    if (!closed) socket.write(bytes);
  });
  client.on('error', close);
  client.on('close', close);
});
console.log(`TazUO binary relay starting: ws://${source} -> tcp://${target}`);
