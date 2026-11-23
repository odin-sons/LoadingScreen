namespace LoadingScreen.Model;

public static class BuiltinConfigText {
  public const string Text = """
      when:
        general:
          - text: { en: "Repairing gear at the right station costs no materials; do it before you leave.", ru: "Ремонт снаряжения у нужной мастерской не требует материалов; чинитесь перед выходом." }
          - text: { en: "The rested bonus improves health and stamina recovery, so comfort pays off beyond the base.", ru: "Бонус отдыха ускоряет восстановление здоровья и выносливости, поэтому уют важен и вне базы." }
          - text: { en: "Food bonuses fade with time; eat fresh before a difficult crossing.", ru: "Эффекты еды со временем слабеют; подкрепитесь перед трудной переправой." }
          - text: { en: "A map mark for your boat is useful when a long walk ends at a different shore.", ru: "Отметка лодки на карте пригодится, если долгий переход закончится на другом берегу." }
          - text: { en: "Mark temporary shelters on the map before you leave them behind.", ru: "Отмечайте временные укрытия на карте, прежде чем уходить дальше." }
          - text: { en: "Use terrain and cover to break a crowded fight into smaller encounters.", ru: "Используйте рельеф и укрытия, чтобы разделить толпу врагов на небольшие группы." }
          - text: { en: "A fire under a roof needs a smoke outlet, even in a spacious room.", ru: "Даже в просторном доме огню под крышей нужен выход для дыма." }
          - text: { en: "Store a second set of food and tools for the trip back to a fallen grave.", ru: "Держите второй набор еды и инструментов для похода к месту гибели." }
          - text: { en: "A hoe can flatten a safe route home before the next storm.", ru: "Мотыга поможет выровнять безопасную дорогу домой до следующей бури." }
            when: {notGlobalKey: "defeated_eikthyr"}
          - text: { en: "A roof over the workbench lets you repair tools without spending materials.", ru: "Крыша над верстаком позволяет чинить инструменты без затрат материалов." }
            when: {notGlobalKey: "defeated_eikthyr"}
          - text: { en: "Mark berry patches on the map; they grow back after you leave them alone.", ru: "Отмечайте ягодные кусты на карте: со временем ягоды вырастут снова." }
            when: {notGlobalKey: "defeated_eikthyr"}
          - text: { en: "A campfire near shelter gives a rested bonus that makes long trips easier.", ru: "Костёр возле укрытия даёт бонус отдыха и облегчает дальние походы." }
            when: {notGlobalKey: "defeated_eikthyr"}
          - text: { en: "Deer startle at noise; approach slowly before drawing the bow.", ru: "Олени пугаются шума; подойдите осторожно, прежде чем натягивать лук." }
            when: {notGlobalKey: "defeated_eikthyr"}
          - text: { en: "Keep a spare meal at home so a failed expedition does not begin with empty food slots.", ru: "Держите дома запасную еду, чтобы после неудачного похода не отправляться без пищи." }
            when: {notGlobalKey: "defeated_eikthyr"}
          - text: { en: "Copper veins often continue below the visible rock; dig around them before moving on.", ru: "Медные жилы часто уходят под землю; раскопайте края, прежде чем искать новую." }
            when: {globalKey: "defeated_eikthyr", notGlobalKey: "defeated_gdking"}
          - text: { en: "Tin appears along Black Forest shores and stream banks, so scout the waterline.", ru: "Олово встречается у берегов и ручьёв Чёрного леса: исследуйте кромку воды." }
            when: {globalKey: "defeated_eikthyr", notGlobalKey: "defeated_gdking"}
          - text: { en: "Surtling cores from burial chambers can start both a kiln and a smelter.", ru: "Ядра суртлингов из погребальных камер пригодятся и для углевыжигательной печи, и для плавильни." }
            when: {globalKey: "defeated_eikthyr", notGlobalKey: "defeated_gdking"}
          - text: { en: "A cart carries heavy ore, but a cleared path keeps it from snagging on roots and stones.", ru: "Повозка везёт тяжёлую руду, но по расчищенной дороге не застрянет на корнях и камнях." }
            when: {globalKey: "defeated_eikthyr", notGlobalKey: "defeated_gdking"}
          - text: { en: "A bronze axe opens up fine wood and the crafting options hidden behind it.", ru: "Бронзовый топор откроет доступ к ценной древесине и новым чертежам." }
            when: {globalKey: "defeated_eikthyr", notGlobalKey: "defeated_gdking"}
          - text: { en: "A portal beside a mining camp saves travel time even though ore must still go by road or sea.", ru: "Портал у шахтёрского лагеря экономит время, хотя руду всё равно придётся везти сушей или морем." }
            when: {globalKey: "defeated_eikthyr", notGlobalKey: "defeated_gdking"}
          - text: { en: "The Swamp key opens sunken crypts; place a portal outside before going in.", ru: "Болотный ключ открывает затонувшие склепы; поставьте портал снаружи перед вылазкой." }
            when: {globalKey: "defeated_gdking", notGlobalKey: "defeated_bonemass"}
          - text: { en: "A hoe can raise a narrow path above swamp water and keep leeches at a distance.", ru: "Мотыгой можно поднять тропу над болотной водой и держаться подальше от пиявок." }
            when: {globalKey: "defeated_gdking", notGlobalKey: "defeated_bonemass"}
          - text: { en: "Poison resistance mead is most useful before the first poisoned hit lands.", ru: "Медовуху защиты от яда лучше выпить до первого отравленного удара." }
            when: {globalKey: "defeated_gdking", notGlobalKey: "defeated_bonemass"}
          - text: { en: "Scrap iron can hide behind muddy scrap piles in crypt doorways and deeper rooms.", ru: "Железный лом скрывается в грязных грудах у входов и в дальних комнатах склепов." }
            when: {globalKey: "defeated_gdking", notGlobalKey: "defeated_bonemass"}
          - text: { en: "Bring a blunt weapon into the swamp; some enemies resist arrows and blades.", ru: "Возьмите на болота дробящее оружие: некоторые враги стойки к стрелам и клинкам." }
            when: {globalKey: "defeated_gdking", notGlobalKey: "defeated_bonemass"}
          - text: { en: "A nearby fire can dry you after the rain; the Wet penalty drains stamina recovery.", ru: "Костёр поможет обсохнуть после дождя: промокание замедляет восстановление выносливости." }
            when: {globalKey: "defeated_gdking", notGlobalKey: "defeated_bonemass"}
          - text: { en: "The wishbone can reveal silver hidden below mountain snow.", ru: "Дужка помогает находить серебро, скрытое под горным снегом." }
            when: {globalKey: "defeated_bonemass", notGlobalKey: "defeated_dragon"}
          - text: { en: "Frost resistance mead buys time on the mountain before warm clothing is ready.", ru: "Медовуха защиты от мороза даст время в горах, пока нет тёплой одежды." }
            when: {globalKey: "defeated_bonemass", notGlobalKey: "defeated_dragon"}
          - text: { en: "Build a sheltered foothold before digging silver; exposed pits invite trouble.", ru: "Перед добычей серебра устройте укрытый лагерь: открытый котлован притягивает неприятности." }
            when: {globalKey: "defeated_bonemass", notGlobalKey: "defeated_dragon"}
          - text: { en: "Obsidian on mountain slopes can improve your arrows before the next boss.", ru: "Обсидиан на горных склонах поможет улучшить стрелы перед следующим боссом." }
            when: {globalKey: "defeated_bonemass", notGlobalKey: "defeated_dragon"}
          - text: { en: "Dragon eggs are heavy; leave room in your pack before climbing to a nest.", ru: "Яйца драконов тяжёлые: освободите место в сумке перед подъёмом к гнезду." }
            when: {globalKey: "defeated_bonemass", notGlobalKey: "defeated_dragon"}
          - text: { en: "A marked route through mountain passes is easier to follow when snow hides the horizon.", ru: "Отмеченный путь через перевал легче найти, когда снег скрывает горизонт." }
            when: {globalKey: "defeated_bonemass", notGlobalKey: "defeated_dragon"}
          - text: { en: "Barley and flax grow in the Plains; move your farm to the right biome.", ru: "Ячмень и лён растут на Равнинах; устройте ферму в подходящем биоме." }
            when: {globalKey: "defeated_dragon", notGlobalKey: "defeated_goblinking"}
          - text: { en: "A windmill turns barley into flour, and stronger wind makes it work faster.", ru: "Мельница превращает ячмень в муку и быстрее работает при сильном ветре." }
            when: {globalKey: "defeated_dragon", notGlobalKey: "defeated_goblinking"}
          - text: { en: "Black metal scrap needs a blast furnace, not the first smelter you built.", ru: "Для лома чёрного металла нужна доменная печь, а не обычная плавильня." }
            when: {globalKey: "defeated_dragon", notGlobalKey: "defeated_goblinking"}
          - text: { en: "Deathsquitos have little health but hit hard; watch the sky on open ground.", ru: "Комары смерти живут недолго, но бьют больно; на открытой местности смотрите по сторонам." }
            when: {globalKey: "defeated_dragon", notGlobalKey: "defeated_goblinking"}
          - text: { en: "A lox pen needs sturdy walls and a quiet place to keep its occupants inside.", ru: "Для загона локсов нужны крепкие стены и спокойное место." }
            when: {globalKey: "defeated_dragon", notGlobalKey: "defeated_goblinking"}
          - text: { en: "Tar pits are dangerous to rush; draw the growths away before gathering tar.", ru: "К смоляным ямам не стоит бросаться: сначала выманите наросты, потом собирайте смолу." }
            when: {globalKey: "defeated_dragon", notGlobalKey: "defeated_goblinking"}
          - text: { en: "A Wisplight parts Mistlands fog, but it does not replace a marked path home.", ru: "Огонёк разгоняет туман Туманных земель, но не заменяет отмеченный путь домой." }
            when: {globalKey: "defeated_goblinking", notGlobalKey: "defeated_queen"}
          - text: { en: "Infested mines hold black cores; search their side rooms carefully.", ru: "Заражённые шахты скрывают чёрные ядра; внимательно осматривайте боковые комнаты." }
            when: {globalKey: "defeated_goblinking", notGlobalKey: "defeated_queen"}
          - text: { en: "Soft tissue is a key ingredient for refined eitr, so keep some for crafting.", ru: "Мягкие ткани нужны для очищенного эйтра; сохраните часть для ремесла." }
            when: {globalKey: "defeated_goblinking", notGlobalKey: "defeated_queen"}
          - text: { en: "Sap extractors work on Yggdrasil roots; protect your route to each one.", ru: "Экстракторы добывают сок из корней Иггдрасиля; защитите путь к каждому." }
            when: {globalKey: "defeated_goblinking", notGlobalKey: "defeated_queen"}
          - text: { en: "The feather cape softens long drops between Mistlands peaks.", ru: "Плащ из перьев смягчает долгие падения между пиками Туманных земель." }
            when: {globalKey: "defeated_goblinking", notGlobalKey: "defeated_queen"}
          - text: { en: "Magecaps support eitr-focused meals; jotun puffs are useful in other recipes.", ru: "Магические шляпки подходят для еды на эйтр, а йотунские клубни пригодятся в других блюдах." }
            when: {globalKey: "defeated_goblinking", notGlobalKey: "defeated_queen"}
          - text: { en: "The Ashlands coast is hostile; prepare a landing site before unloading supplies.", ru: "Берег Пепельных земель враждебен; подготовьте место высадки до разгрузки припасов." }
            when: {globalKey: "defeated_queen", notGlobalKey: "defeated_fader"}
          - text: { en: "A stone portal can move materials that an ordinary portal leaves behind.", ru: "Каменный портал переносит материалы, которые не пропускает обычный." }
            when: {globalKey: "defeated_queen", notGlobalKey: "defeated_fader"}
          - text: { en: "Lava demands a safe route and the right gear; do not treat it like shallow water.", ru: "Для лавы нужны безопасный маршрут и подходящее снаряжение; это не мелководье." }
            when: {globalKey: "defeated_queen", notGlobalKey: "defeated_fader"}
          - text: { en: "Grausten makes sturdy building material for a foothold in the Ashlands.", ru: "Граустен подходит для прочного укрепления в Пепельных землях." }
            when: {globalKey: "defeated_queen", notGlobalKey: "defeated_fader"}
          - text: { en: "Ashlands enemies can gather quickly; leave room to retreat from a fight.", ru: "Враги Пепельных земель могут быстро собраться вместе; оставляйте путь к отступлению." }
            when: {globalKey: "defeated_queen", notGlobalKey: "defeated_fader"}
          - text: { en: "A protected portal near the shore makes a lost expedition easier to recover.", ru: "Защищённый портал у берега облегчит возвращение после неудачной вылазки." }
            when: {globalKey: "defeated_queen", notGlobalKey: "defeated_fader"}
          - text: { en: "The Deep North is a full biome; scout a safe shore before committing your supplies.", ru: "Глубокий Север — полноценный биом; найдите безопасный берег до разгрузки припасов." }
            when: {globalKey: "defeated_fader"}
          - text: { en: "Keep frost protection ready when crossing exposed northern terrain.", ru: "Держите защиту от мороза наготове при переходе открытых северных земель." }
            when: {globalKey: "defeated_fader"}
          - text: { en: "Mark shelter and landing points before a snowstorm erases distant landmarks.", ru: "Отмечайте укрытия и места высадки до того, как метель скроет ориентиры." }
            when: {globalKey: "defeated_fader"}
          - text: { en: "Bring materials for a sheltered forward camp, not just weapons.", ru: "Берите материалы для защищённого передового лагеря, а не только оружие." }
            when: {globalKey: "defeated_fader"}
          - text: { en: "A spare route back matters when cold, rough ground, and enemies meet.", ru: "Запасной путь домой пригодится, когда холод, сложная местность и враги встретятся вместе." }
            when: {globalKey: "defeated_fader"}
          - text: { en: "Keep old gear and food stocked at home for a safer recovery run.", ru: "Храните дома запасное снаряжение и еду для безопасного возвращения за вещами." }
            when: {globalKey: "defeated_fader"}
        biome:
          Meadows:
            - text: { en: "An abandoned house can become shelter after a roof and workbench repair.", ru: "Заброшенный дом станет укрытием, если починить крышу и поставить верстак." }
            - text: { en: "Flint lies near water; scan the shore while gathering early supplies.", ru: "Кремень лежит у воды; осматривайте берег, собирая первые припасы." }
          BlackForest:
            - text: { en: "Copper is easier to haul when a path to the coast is cleared first.", ru: "Медь проще вывозить, если заранее расчистить дорогу к берегу." }
            - text: { en: "Burial chambers are worth marking even after one visit; missed rooms can hold cores.", ru: "Отмечайте погребальные камеры даже после посещения: в пропущенных комнатах могут быть ядра." }
          Swamp:
            - text: { en: "Keep a raised route between crypts to avoid fighting in deep water.", ru: "Проложите приподнятую тропу между склепами, чтобы не драться в глубокой воде." }
            - text: { en: "A poison resistance drink works best before you enter the swamp.", ru: "Защиту от яда лучше выпить до входа на болота." }
          Mountain:
            - text: { en: "Use a wishbone while exploring snowy slopes; silver can be buried out of sight.", ru: "Исследуйте заснеженные склоны с дужкой: серебро может быть скрыто под землёй." }
            - text: { en: "Carry frost protection and mark the descent before clouds cover the path.", ru: "Возьмите защиту от мороза и отметьте спуск, пока облака не скрыли тропу." }
          Plains:
            - text: { en: "Keep barley and flax fields here; moving the harvest home is easier than moving the crop.", ru: "Выращивайте ячмень и лён здесь: урожай перевезти домой проще, чем сами посевы." }
            - text: { en: "Look for deathsquitos before crossing an open field with low health.", ru: "Перед переходом открытого поля проверьте, нет ли комаров смерти, особенно при малом здоровье." }
          Mistlands:
            - text: { en: "A Wisplight clears nearby fog; map markers still help you navigate the ridges.", ru: "Огонёк рассеивает туман рядом, а отметки на карте помогут пройти по хребтам." }
            - text: { en: "Use the high ground to spot paths around steep walls and deep valleys.", ru: "Поднимайтесь выше, чтобы увидеть путь вокруг крутых стен и глубоких долин." }
          AshLands:
            - text: { en: "Secure a sheltered beachhead before carrying your supplies inland.", ru: "Закрепитесь на защищённом участке берега, прежде чем нести припасы вглубь." }
            - text: { en: "Keep an escape route ready when crossing lava fields.", ru: "При переходе лавовых полей заранее подготовьте путь отступления." }
          DeepNorth:
            - text: { en: "Snow can hide the shape of the ground; move carefully through unfamiliar passes.", ru: "Снег скрывает рельеф; осторожно идите по незнакомым перевалам." }
            - text: { en: "Set a sheltered return point before exploring far from the northern coast.", ru: "Устройте защищённую точку возвращения до похода далеко от северного берега." }
          Ocean:
            - text: { en: "Watch wind direction before a long crossing; the return trip may be slower.", ru: "Проверьте направление ветра перед долгим плаванием: обратный путь может быть медленнее." }
            - text: { en: "Keep a spare landing place marked in case the first shore is unsafe.", ru: "Отметьте запасное место высадки на случай, если первый берег окажется опасным." }
        death:
          Undefined:
            - text: { en: "Check what damaged you before repeating the route; the combat log can offer a clue.", ru: "Прежде чем идти тем же путём, проверьте причину урона в журнале боя." }
          EnemyHit:
            - text: { en: "Pull one enemy away from the group before trying to recover your gear.", ru: "Выманите одного врага из группы, прежде чем возвращаться за вещами." }
          PlayerHit:
            - text: { en: "Agree on safe fighting space when friendly fire or PvP can reach a companion.", ru: "Договоритесь о безопасном месте боя, если союзника может задеть ваш удар." }
          Fall:
            - text: { en: "A steep descent can be safer in stages; look for ledges before jumping.", ru: "Крутой спуск безопаснее проходить поэтапно; ищите уступы перед прыжком." }
          Drowning:
            - text: { en: "Keep enough stamina to swim back; turning around early beats a last-second sprint.", ru: "Оставляйте силы на обратный заплыв: лучше развернуться заранее." }
          Burning:
            - text: { en: "Step out of flames before healing; another burn can undo the recovery.", ru: "Выйдите из огня до лечения: новый ожог сведёт его на нет." }
          Freezing:
            - text: { en: "Carry frost protection before entering cold regions, especially after a death run.", ru: "Берите защиту от мороза в холодные края, особенно когда идёте за вещами." }
          Poisoned:
            - text: { en: "Poison resistance is preparation, not a cure after the damage has begun.", ru: "Защита от яда нужна заранее: она не отменит уже полученный урон." }
          Water:
            - text: { en: "Treat dangerous water as a route hazard and find a safer crossing.", ru: "Опасную воду лучше обойти или найти более безопасную переправу." }
          Smoke:
            - text: { en: "Give a fire a vent; smoke can collect under a low roof.", ru: "Оставьте дыму выход: под низкой крышей он скапливается." }
          EdgeOfWorld:
            - text: { en: "The world's edge is no shortcut; turn back while there is room to sail.", ru: "Край мира не служит коротким путём; разворачивайтесь, пока есть место для манёвра." }
          Impact:
            - text: { en: "Give moving objects and heavy blows more space than they seem to need.", ru: "Держитесь дальше от движущихся предметов и сильных ударов, чем кажется нужным." }
          Cart:
            - text: { en: "Slow a loaded cart on slopes and keep clear of its path.", ru: "Тормозите гружёную повозку на склонах и не стойте у неё на пути." }
          Tree:
            - text: { en: "Watch the whole trunk when felling a tree; it can roll after it lands.", ru: "Следите за стволом при рубке: после падения он может покатиться." }
          Self:
            - text: { en: "Check the reach of your own tools and effects before using them in tight spaces.", ru: "Проверьте радиус действия своих инструментов и эффектов в тесном месте." }
          Structural:
            - text: { en: "A collapsing structure can hurt; reinforce the supports before dismantling it.", ru: "Обрушение постройки опасно; укрепите опоры перед разборкой." }
          Turret:
            - text: { en: "Find cover and break the turret's line of sight before crossing open ground.", ru: "Найдите укрытие и прервите линию огня турели перед выходом на открытое место." }
          Boat:
            - text: { en: "Keep clear of a boat when waves or a shore can shove it sideways.", ru: "Не стойте вплотную к лодке, когда волны или берег могут сдвинуть её вбок." }
          Stalagtite:
            - text: { en: "Look overhead in caves; falling stone leaves little time to react.", ru: "Смотрите вверх в пещерах: падающий камень почти не оставляет времени на реакцию." }
          Catapult:
            - text: { en: "Give a catapult's landing area space, even when you are not the target.", ru: "Не стойте в зоне падения снаряда катапульты, даже если целятся не в вас." }
          CinderFire:
            - text: { en: "Leave burning cinders quickly and avoid retreating through the same patch.", ru: "Быстро уходите от горящих углей и не отступайте через то же место." }
          AshlandsOcean:
            - text: { en: "Plan the Ashlands sea crossing with a safe landing in mind.", ru: "Планируйте переход через море Пепельных земель, заранее выбрав место высадки." }
          AshlandsLava:
            - text: { en: "Lava is a route to plan, not a shortcut to improvise.", ru: "Путь через лаву нужно планировать заранее, а не искать на ходу." }
          Incinerator:
            - text: { en: "Stand clear when disposing of items; the incinerator is not a storage chest.", ru: "Отойдите при сжигании вещей: мусоросжигатель — не сундук." }
          DrawBridge:
            - text: { en: "Wait for a drawbridge to finish moving before stepping into its path.", ru: "Подождите, пока подъёмный мост закончит движение, прежде чем идти под ним." }
          Blunt:
            - text: { en: "Space out heavy strikes and keep stamina for a block or dodge.", ru: "Не тратьте всю выносливость на сильные удары: оставьте её на блок или уклонение." }
          Slash:
            - text: { en: "Watch an enemy's full swing before closing in from the side.", ru: "Дождитесь конца широкого замаха врага, прежде чем заходить сбоку." }
          Pierce:
            - text: { en: "Break line of sight with ranged attackers before recovering your grave.", ru: "Скройтесь от стрелков за укрытием, прежде чем идти к месту гибели." }
          Chop:
            - text: { en: "Keep your distance from chopping attacks and nearby falling timber.", ru: "Держитесь подальше от рубящих ударов и падающих деревьев." }
          Pickaxe:
            - text: { en: "Move out of the mining strike's path before closing in.", ru: "Выйдите из зоны удара киркой, прежде чем сближаться." }
          Fire:
            - text: { en: "A fire resistance drink can help when flames are part of the next fight.", ru: "Медовуха защиты от огня поможет, если в следующем бою ждёт пламя." }
          Frost:
            - text: { en: "Cold damage calls for protection and a route with cover.", ru: "От урона холодом помогут защита и путь с укрытиями." }
          Lightning:
            - text: { en: "Leave room to dodge when lightning attacks begin to charge.", ru: "Оставьте место для уклонения, когда начинается заряд молнии." }
          Poison:
            - text: { en: "Bring poison resistance before facing enemies that spread toxins.", ru: "Возьмите защиту от яда до встречи с врагами, которые отравляют." }
          Spirit:
            - text: { en: "Prepare for spirit damage before returning to the same fight.", ru: "Подготовьтесь к урону духом, прежде чем повторять тот же бой." }
        sleep:
          - text: { en: "A bed needs shelter and a nearby fire to be a reliable place to rest.", ru: "Для надёжного отдыха кровати нужны крыша и огонь поблизости." }
          - text: { en: "More comfort extends the rested bonus you carry into tomorrow's journey.", ru: "Чем уютнее дом, тем дольше бонус отдыха в завтрашнем походе." }
          - text: { en: "Store food and repair gear at night so the morning starts ready.", ru: "Сложите еду в запас и почините снаряжение вечером, чтобы утром быть готовыми." }
          - text: { en: "A nearby portal makes home a practical stop between distant expeditions.", ru: "Портал рядом с домом превращает его в удобную остановку между дальними походами." }
      """;
}
