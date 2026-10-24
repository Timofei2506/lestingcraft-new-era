import test from 'node:test';
import assert from 'node:assert/strict';
import {serverName,serverRecord,publicServer,sortServers,ServerDirectory,MAX_SERVERS} from '../src/server-registry.js';
import {createConfig} from '../src/protocol.js';

const config=(overrides={})=>createConfig({version:1,rulesVersion:14,seed:2506,dimension:0,mode:0,ownerKey:'a'.repeat(32),...overrides});

test('server names are printable, single spaced and bounded', () => {
  assert.equal(serverName('  Тихий   Брод  '),'Тихий Брод');
  assert.equal(serverName('Мир\nс другом'),'Мир с другом');
  assert.equal(serverName(''),'Новый мир');
  assert.equal(serverName(null),'Новый мир');
  assert.equal(serverName('💚 Лес'),'💚 Лес');
  assert.throws(()=>serverName('x'),/2-32/);
  assert.throws(()=>serverName('я'.repeat(33)),/2-32/);
  assert.equal(serverName('я'.repeat(32)).length,32);
});

test('published servers carry the world identity a client needs', () => {
  const room=config({seed:-77,mode:2});
  const record=serverRecord(room,'Мой мир',1792847000000,room.ownerKey);
  assert.deepEqual(record,{name:'Мой мир',mode:2,seed:-77,dimension:0,rulesVersion:14,contentVersion:9,createdAt:1792847000000,owner:room.ownerKey});
  const listed=publicServer('a'.repeat(32),record,{players:3,maxPlayers:8,started:true});
  assert.equal(listed.name,'Мой мир');
  assert.equal(listed.players,3);
  assert.equal(listed.started,true);
  assert.equal(listed.offline,false);
  assert.equal(listed.seed,-77);
});

test('a server whose room is not loaded is still listed but marked offline', () => {
  const listed=publicServer('b'.repeat(32),serverRecord(config(),'Сон',1),null);
  assert.equal(listed.offline,true);
  assert.equal(listed.players,0);
  assert.equal(listed.maxPlayers,8);
});

test('the directory keeps newest first, dedupes codes and evicts the oldest', () => {
  const directory=new ServerDirectory();
  for(let i=0;i<MAX_SERVERS+5;i++)
    directory.add(String(i).padStart(32,'0'),serverRecord(config(),'Сервер '+i,i));
  assert.equal(directory.entries.length,MAX_SERVERS);
  assert.equal(directory.get('0'.repeat(32)),null,'the oldest entries are evicted');
  directory.add('0'.repeat(32),serverRecord(config(),'Повтор',1));
  assert.equal(directory.entries.length,MAX_SERVERS,'dedupe keeps the directory bounded');
  assert.equal(directory.entries.filter(e=>e.code==='0'.repeat(32)).length,1);
});

test('only the creator can unpublish a server', () => {
  const directory=new ServerDirectory();
  const record=serverRecord(config(),'Общий мир',1,'c'.repeat(32));
  directory.add('d'.repeat(32),record);
  assert.throws(()=>directory.remove('d'.repeat(32),'e'.repeat(32)),/creator/);
  assert.equal(directory.get('d'.repeat(32)).name,'Общий мир');
  assert.equal(directory.remove('d'.repeat(32),'c'.repeat(32)),true);
  assert.equal(directory.get('d'.repeat(32)),null);
  assert.equal(directory.remove('f'.repeat(32),'c'.repeat(32)),false);
});

test('the listing sorts busy worlds first and then the newest', () => {
  const rows=[
    {code:'1',name:'A',players:0,createdAt:10},
    {code:'2',name:'B',players:4,createdAt:5},
    {code:'3',name:'C',players:0,createdAt:20},
  ];
  assert.deepEqual(sortServers(rows).map(r=>r.name),['B','C','A']);
});

test('the directory survives a round trip through JSON', () => {
  const directory=new ServerDirectory();
  directory.add('a'.repeat(32),serverRecord(config(),'Лес',7,'b'.repeat(32)));
  const restored=new ServerDirectory(JSON.parse(JSON.stringify(directory.toJSON())));
  assert.equal(restored.get('a'.repeat(32)).name,'Лес');
  assert.equal(restored.get('a'.repeat(32)).owner,'b'.repeat(32));
  assert.deepEqual(new ServerDirectory([{code:'short'}]).entries,[]);
});
