// Shared Node / Workers(nodejs_compat). Passwords and recovery codes are never logged or returned in profiles.
import {randomBytes,timingSafeEqual,createHash} from 'node:crypto';
import {Buffer} from 'node:buffer';
import {derivePassword as derive,PASSWORD_KDF} from './password-kdf.js';
import {achievements,counterKeys,levelFor} from './achievements.js';
const random=()=>randomBytes(32).toString('hex');export const digest=s=>createHash('sha256').update(s).digest('hex');
const equal=(a,b)=>typeof a==='string'&&typeof b==='string'&&a.length===64&&b.length===64&&timingSafeEqual(Buffer.from(a,'hex'),Buffer.from(b,'hex'));
const fail=(message,status=400)=>{throw Object.assign(new Error(message),{status});};
const nameOf=v=>{if(typeof v!=='string'||! /^[a-zA-Z0-9_]{3,24}$/.test(v))fail('Логин: 3–24 латинские буквы, цифры или _.');return v.toLowerCase();};
const passwordOf=v=>{if(typeof v!=='string'||v.length<12||v.length>128||Buffer.byteLength(v)>512)fail('Пароль: от 12 до 128 символов.');return v;};
const statKeys=new Set([...counterKeys,'deaths','damage_taken']);
export function publicProfile(u){return {uid:u.uid,username:u.username,title:u.title||'',titleName:achievements.find(a=>a.id===u.title)?.title||'Странник',xp:u.xp,level:levelFor(u.xp),achievements:u.earned,stats:Object.entries(u.stats).sort().map(([key,value])=>({key,value})),integrity:'local-and-friends',revision:u.revision};}
export class AccountService {
 constructor(store,clock=()=>Date.now()){this.store=store;this.clock=clock;this.tail=Promise.resolve();}
 handle(path,method,body={},token='',ip='local'){const work=this.tail.then(()=>this.run(path,method,body,token,ip));this.tail=work.catch(()=>{});return work;}
 async run(path,method,b,token,ip){
  const s=this.store,now=this.clock();if(!b||typeof b!=='object'||Array.isArray(b))fail('Ожидается JSON-объект.');
  if(!s.limit('ip:'+digest(ip),now,80,60000))fail('Слишком много запросов.',429);
  const auth=async()=>{if(! /^[a-f0-9]{64}$/.test(token))fail('Войдите в аккаунт.',401);const session=s.session(digest(token));if(!session||session.expires<=now)fail('Сессия истекла. Войдите снова.',401);const u=s.user(session.username);if(!u||u.uid!==session.uid)fail('Сессия недействительна.',401);return u;};
  const issue=async(u,recoveryCode='')=>{const token=random();s.putSession(digest(token),{username:u.username,uid:u.uid,expires:now+7*86400000,created:now});s.trimSessions(u.uid,now);await s.flush();return {ok:true,token,recoveryCode,profile:publicProfile(u)};};
  if(path==='register'&&method==='POST'){
   const username=nameOf(b.username),password=passwordOf(b.password);
   // 20/hour per address, not 3: friends and classmates often share one NAT address,
   // and the goal is that a group can start playing together. Mass creation stays capped.
   if(!s.limit('registration:'+digest(ip),now,20,3600000))fail('Лимит регистраций: попробуйте позже.',429);
   if(s.user(username))fail('Этот логин недоступен.',409);
   const salt=random(),hash=await derive(password,salt,PASSWORD_KDF),recoveryCode=random();
   const u={username,uid:randomBytes(16).toString('hex'),salt,hash,kdf:PASSWORD_KDF,recovery:digest(recoveryCode),stats:{},earned:[],xp:0,title:'',revision:0,batches:[],created:now};s.putUser(u);return issue(u,recoveryCode);
  }
  if(path==='login'&&method==='POST'){
   const username=nameOf(b.username);passwordOf(b.password);
   if(!s.limit('login:'+username,now,8,900000)||!s.limit('loginip:'+digest(ip),now,30,900000))fail('Подождите перед следующей попыткой входа.',429);
   const u=s.user(username),hash=await derive(b.password,u?.salt||'00'.repeat(32),u?u.kdf:PASSWORD_KDF);if(!u||!equal(hash,u.hash))fail('Неверный логин или пароль.',401);return issue(u);
  }
  if(path==='recover'&&method==='POST'){
   const username=nameOf(b.username);passwordOf(b.password);
   if(!s.limit('recovery:'+username,now,5,3600000))fail('Слишком много попыток восстановления.',429);
   const u=s.user(username);if(!u||typeof b.recoveryCode!=='string'||!equal(digest(b.recoveryCode),u.recovery))fail('Неверный логин или код восстановления.',401);
   u.salt=random();u.hash=await derive(b.password,u.salt,PASSWORD_KDF);u.kdf=PASSWORD_KDF;const code=random();u.recovery=digest(code);s.deleteSessions(u.uid);s.putUser(u);return issue(u,code);
  }
  const u=await auth();
  if(path==='me'&&method==='GET')return {ok:true,profile:publicProfile(u)};
  if(path==='logout'&&method==='POST'){s.deleteSession(digest(token));await s.flush();return {ok:true};}
  if(path==='password'&&method==='POST'){
   passwordOf(b.password);passwordOf(b.newPassword);if(!equal(await derive(b.password,u.salt,u.kdf),u.hash))fail('Неверный пароль.',401);u.salt=random();u.hash=await derive(b.newPassword,u.salt,PASSWORD_KDF);u.kdf=PASSWORD_KDF;s.deleteSessions(u.uid);s.putUser(u);return issue(u);
  }
  if(path==='delete'&&method==='POST'){passwordOf(b.password);if(!equal(await derive(b.password,u.salt,u.kdf),u.hash))fail('Неверный пароль.',401);s.deleteSessions(u.uid);s.deleteUser(u.username);await s.flush();return {ok:true};}
  if(path==='profile'&&method==='POST'){
   if(typeof b.title!=='string'||b.title!==''&&!u.earned.includes(b.title)||b.title!==''&&!achievements.find(a=>a.id===b.title)?.title)fail('Этот титул ещё не открыт.');u.title=b.title;u.revision++;s.putUser(u);await s.flush();return {ok:true,profile:publicProfile(u)};
  }
  if(path==='progress'&&method==='POST'){
   if('xp' in b||'level' in b||'uid' in b||'achievements' in b)fail('XP, уровень и награды вычисляет сервер.');
   if(typeof b.deviceId!=='string'||! /^[a-f0-9]{32}$/.test(b.deviceId)||!Number.isSafeInteger(b.sequence)||b.sequence<1||typeof b.batchId!=='string'||! /^[a-f0-9]{32}$/.test(b.batchId)||!Array.isArray(b.events)||b.events.length>128)fail('Неверный пакет статистики.');
   u.devices=u.devices||{};if(b.sequence<=(u.devices[b.deviceId]||0))return {ok:true,batchId:b.batchId,profile:publicProfile(u)};
   if(b.sequence!==(u.devices[b.deviceId]||0)+1)fail('Нарушена последовательность пакетов.',409);if(!Object.hasOwn(u.devices,b.deviceId)&&Object.keys(u.devices).length>=64)fail('Лимит устройств профиля.',429);
   if(!s.limit('progress:'+u.uid,now,30,60000))fail('Слишком частая синхронизация.',429);
   for(const e of b.events)if(!e||!statKeys.has(e.key)||!Number.isSafeInteger(e.value)||e.value<0||e.value>1000000||!['add','max'].includes(e.mode))fail('Неверное событие статистики.');
   for(const e of b.events)u.stats[e.key]=Math.min(2000000000,e.mode==='max'?Math.max(u.stats[e.key]||0,e.value):(u.stats[e.key]||0)+e.value);
   for(const a of achievements)if((u.stats[a.counter]||0)>=a.target&&!u.earned.includes(a.id)){u.earned.push(a.id);u.xp+=a.xp;}
   u.devices[b.deviceId]=b.sequence;u.batches.push(b.batchId);if(u.batches.length>1024)u.batches.shift();u.revision++;s.putUser(u);await s.flush();return {ok:true,batchId:b.batchId,profile:publicProfile(u)};
  }
  fail('Маршрут не найден.',404);
 }
}
// Pure in-memory store is also used by the standalone persistent adapter and regression tests.
export class MemoryAccountStore {
 constructor(data={}){this.data={users:{},sessions:{},limits:{},...data};}
 user(n){return Object.hasOwn(this.data.users,n)?structuredClone(this.data.users[n]):null;}putUser(u){Object.defineProperty(this.data.users,u.username,{value:structuredClone(u),enumerable:true,writable:true,configurable:true});}deleteUser(n){delete this.data.users[n];}
 session(h){return this.data.sessions[h];}putSession(h,s){this.data.sessions[h]=s;}deleteSession(h){delete this.data.sessions[h];}deleteSessions(uid){for(const [h,s] of Object.entries(this.data.sessions))if(s.uid===uid)delete this.data.sessions[h];}
 trimSessions(uid,now){const own=Object.entries(this.data.sessions).filter(([h,s])=>s.uid===uid).sort((a,b)=>b[1].created-a[1].created);for(const [h,s] of Object.entries(this.data.sessions))if(s.expires<=now||own.slice(5).some(x=>x[0]===h))delete this.data.sessions[h];}
 limit(key,now,max,window){for(const k in this.data.limits)if(this.data.limits[k].until<=now)delete this.data.limits[k];const v=this.data.limits[key]||{until:now+window,count:0};v.count++;this.data.limits[key]=v;return v.count<=max;}
 async flush(){}
}
