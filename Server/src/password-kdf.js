// Standard Argon2id (OWASP minimum 19 MiB, t=2, p=1). Runs in free Workers
// without their production PBKDF2 iteration ceiling. Never weaken a legacy hash.
import {argon2idAsync} from '@noble/hashes/argon2.js';
import {pbkdf2} from 'node:crypto';
import {Buffer} from 'node:buffer';
export const PASSWORD_KDF='argon2id-v1';
export const LEGACY_KDF='pbkdf2-sha256-600000';
export async function derivePassword(password,salt,kdf=LEGACY_KDF){
 if(kdf===PASSWORD_KDF){
  const key=await argon2idAsync(password,Buffer.from(salt,'hex'),{t:2,m:19456,p:1,dkLen:32,asyncTick:16});
  try{return Buffer.from(key).toString('hex');}finally{key.fill(0);}
 }
 if(kdf!==LEGACY_KDF)throw new Error('Unsupported stored password KDF');
 // Legacy standalone-server accounts remain compatible. Recovery/password
 // rotation always writes Argon2id; no 100k-PBKDF2 downgrade on production.
 return new Promise((ok,no)=>pbkdf2(password,Buffer.from(salt,'hex'),600000,32,'sha256',(e,b)=>e?no(e):ok(b.toString('hex'))));
}
