import test from 'node:test';import assert from 'node:assert/strict';import {RoomState,newPeer,createConfig} from '../src/protocol.js';
const room=()=>new RoomState(createConfig({seed:1,dimension:0,version:2,rulesVersion:3,mode:0}));
const send=(r,p,message,now=10000)=>r.handle(p,JSON.stringify({type:'chat',name:'spoof',message}),now,()=>{throw Error('Chat must not persist edits');});
test('chat uses server name, not user supplied identity',()=>{const p=newPeer('id','Alice');assert.deepEqual(send(room(),p,'Привет 👋'),{type:'chat',id:'id',name:'Alice',message:'Привет 👋'});});
test('chat strips markup and controls',()=>assert.equal(send(room(),newPeer('id','A'),'<hello>\nworld').message,'helloworld'));
test('chat rejects empty, long and non-string messages',()=>{for(const m of ['   ','a'.repeat(241),{a:1},null])assert.throws(()=>send(room(),newPeer('id','A'),m));});
test('chat cooldown allows next message after a second',()=>{const r=room(),p=newPeer('id','A');send(r,p,'one');assert.throws(()=>send(r,p,'two',10500),/cooldown/);assert.equal(send(r,p,'three',11000).message,'three');});
test('chat never changes room revisions or saves',()=>{const r=room(),p=newPeer('id','A');send(r,p,'Привет');assert.equal(r.config.rev,0);assert.equal(r.savedPlayers.size,0);assert.equal(r.edits.size,0);});
