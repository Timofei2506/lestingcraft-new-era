from model_io import load_catalog,save_catalog
from pathlib import Path
import json
R=Path(__file__).resolve().parents[1];models=load_catalog(R);names={m['id']:m['name'] for m in models['models']};names[18]='Доски';all=[]
def category(name,rows):
 assert len(rows)==20,(name,len(rows))
 for index,row in enumerate(rows):
  title,key,target,description=row;id=f'a{len(all)+1:03}';all.append(dict(id=id,name=title,category=name,counter=key,target=target,description=description,xp=40+(index//5)*30,title=({'Первые шаги':'Искра','Минералы':'Искатель алмазов','Строительство':'Снежный строитель','Оружейная':'Стальной путник','Исследование':'Ночной дозор','Выживание':'Запасливый','Охота':'Легенда охоты','Мастерские':'Стеклодув','История и друзья':'Попутчик','Долгая экспедиция':'Алмазное сердце'}[name]) if index==19 else ''))
def itemrow(prefix,id,key):return (prefix+names[id],key+str(id),1,'Получите результат действием: '+names[id]+'. Творческий режим не учитывается.')
category('Первые шаги',[itemrow('Своими руками: ',i,'craft_') for i in [120,121,100,101,102,103,15,104,16,17,114,130,131,132,36,37,38,35,40,148]])
category('Минералы',[itemrow('Первая находка: ',i,'mine_') for i in [1,4,5,6,13,19,20,21,22,23,24,25,26,27,28,29,30,31,33,47]])
category('Строительство',[itemrow('Основа дома: ',i,'place_') for i in [1,4,18,16,17,15,44,37,38,30,42,27,41,46,45,2,39,35,43,14]])
category('Оружейная',[itemrow('Кузница: ',i,'craft_') for i in list(range(150,166))+[105,106,107,108]])
rows=[]
for i,n in enumerate(['Деревня с колодцем','Пограничный замок','Разрушенная усадьба','Дозорная башня','Заброшенный лагерь']):rows.append(('За горизонтом: '+n,'structure_'+str(i),1,'Посетите сгенерированную структуру: '+n+'.'))
rows.append(('Подпорки прошлого','mine_visit',1,'Войдите в заброшенную шахту, связанную с входной пещерой.'))
for v in [100,1000,5000,10000,50000]:rows.append((f'Дальняя дорога · {v} м','walk_m',v,f'Пройдите {v} метров без телепортации.'))
for v in [60,600,1800]:rows.append((f'Под сводами · {v//60} мин','cave_s',v,f'Проведите {v//60} минут под землёй.'))
for v in [30,180]:rows.append((f'Холодная вода · {v} с','swim_s',v,f'Проведите {v} секунд в воде.'))
for v in [100,1000,5000]:rows.append((f'На своих двоих · {v} м','sprint_m',v,f'Пробегите {v} метров.'))
rows.append(('Ночной дозор','night_s',600,'Проведите 10 минут в ночном мире.'))
category('Исследование',rows)
rows=[]
for key,vs,n in [('alive_s',[60,600,1800,3600,10800,36000],'Время жизни'),('shelter_s',[60,600,1800],'Под крышей'),('fire_s',[60,300,900],'У тёплого огня'),('rain_s',[60,300,900],'Под дождём'),('food',[1,10,50,200,500],'Запасы в дорогу')]:
 for v in vs:rows.append((f'{n} · {v}',''+key,v,f'{n}: накопите {v} '+('съеденных порций.' if key=='food' else 'секунд в подходящих условиях.')))
category('Выживание',rows)
rows=[]
for species,n in [(0,'Олень'),(1,'Заяц'),(2,'Кабан')]:
 for v in [1,5,25,100,250]:rows.append((f'{n}: охотничий след {v}','kill_'+str(species),v,f'Добудьте {v} животных вида «{n}». Засчитывается при получении личной добычи.'))
for v in [10,50,100,500,1000]:rows.append((f'Охотничий журнал · {v}','kills',v,f'Добудьте {v} животных.'))
category('Охота',rows)
rows=[]
for v in [1,10,50,100,500]:rows.append((f'Горячая работа · {v}','smelt',v,f'Получите {v} результатов обработки в работающей печи.'))
for id,vs in [(140,[1,25,100]),(141,[1,25]),(142,[1,10]),(143,[1,10]),(144,[1,10]),(138,[1,50]),(44,[1,100])]:
 for v in vs:rows.append((f'{names[id]}: у печи {v}','smelt_'+str(id),v,f'Выплавьте / приготовьте {v}: {names[id]}.'))
category('Мастерские',rows)
stages=['Обломки остались позади','Последние слова','Дорога к переправе','Тихий Брод','Слова у колодца','К старой мельнице','Печати и старая песня','Сердечник кузнецу','Корневое святилище','Разбуженный хранитель','Когда стихли корни','Цена закрытой двери','Пока горит последний дом']
rows=[(n,'story_'+str(i),1,f'Дойдите до этапа {i} общей первой главы. Нет срока выполнения.') for i,n in enumerate(stages,2)]
rows += [('Семь на замке','bridge',1,'Решите головоломку противовесов.'),('Вспомнить песню','mill',1,'Откройте три печати мельницы.'),('Тот, кто открывает','altar',1,'Добровольно разбудите Стража.'),('Янтарное окно','pylons',1,'Откройте сердце тремя жаровнями.'),('Хранитель остановлен','bosses',1,'Победите Стража в общей главе.'),('Первое слово','chat',1,'Отправьте сообщение товарищам в сетевой игре.'),('Не один у костра','coop_s',60,'Проведите минуту в комнате с другим игроком.')]
category('История и друзья',rows)
rows=[]
for key,vs,n in [('mined',[10,100,1000,10000],'Добытчик'),('placed',[10,100,1000,5000],'Архитектор'),('crafted',[10,100,500,2000],'Умелые руки'),('mine_4',[16,64,256],'Лесоруб'),('mine_5',[16,64],'Зелёный навес'),('mine_47',[10,50,200],'Алмазная глубина')]:
 for v in vs:rows.append((f'{n} · {v}',key,v,f'{n}: выполните {v} соответствующих действий в выживании.'))
category('Долгая экспедиция',rows)
assert len(all)==200 and len({(a['counter'],a['target']) for a in all})==200
(R/'Assets/Resources/Achievements.json').write_text(json.dumps({'version':1,'achievements':all},ensure_ascii=False,separators=(',',':')))
(R/'Server/src/achievements.js').write_text('export const achievements='+json.dumps(all,ensure_ascii=False,separators=(',',':'))+';\nexport const counterKeys=new Set(achievements.map(a=>a.counter));\nexport const levelFor=xp=>1+Math.floor((Math.sqrt(1+xp/25)-1)/2);\n')
(R/'Docs/Достижения-200.md').write_text('# 200 достижений\n\nТолько события существующих систем. Без будущих боссов, приручения и несуществующих культур. Creative исключён. Уровень вычисляется сервером из XP достижений; титулы открываются последним достижением категории. Статистика локального выживания доверенная, не античит.\n\n|ID|Категория|Название|Условие|XP|\n|---|---|---|---|---|\n'+''.join(f'|{a["id"]}|{a["category"]}|{a["name"]}|{a["description"]}|{a["xp"]}|\n' for a in all))
print('200 unique achievements,',len({a['counter'] for a in all}),'tracked counter keys')
