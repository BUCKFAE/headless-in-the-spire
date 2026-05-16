# Seed 42 Recon — Ironclad

- character: `Ironclad`
- seed: `42`
- starting relics: `BURNING_BLOOD`

## Floor 0: MapRoom  (hp=80/80)

  - map options: (3,0):Monster, (0,1):Monster, (3,1):Monster, (5,1):Monster

  - bag: (empty)
## Floor 2: CombatRoom  (hp=80)


  → pick map (0,1) → CombatRoom floor=2
### Combat #1 on floor 2

#### Round 1  (e=3/3 block=0 draw=5 disc=0)

  - hand:
    - [0] `Bash` cost=2 canPlay=True target=AnyEnemy
    - [1] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [2] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [3] `DefendIronclad` cost=1 canPlay=True target=Self
    - [4] `DefendIronclad` cost=1 canPlay=True target=Self
  - enemies:
    - [0] `FUZZY_WURM_CRAWLER` 55/55 block=0 → Attack 4×1

  → play card [0] target=0
  → play card [0] target=0
  → end_turn → round transition
#### Round 2  (e=3/3 block=0 draw=0 disc=5)

  - hand:
    - [0] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [1] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [2] `DefendIronclad` cost=1 canPlay=True target=Self
    - [3] `DefendIronclad` cost=1 canPlay=True target=Self
    - [4] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `FUZZY_WURM_CRAWLER` 38/55 block=0 → Buff  powers=[VULNERABLE_POWER:1]

  → play card [0] target=0
  → play card [0] target=0
  → play card [0]
  → end_turn → round transition
#### Round 3  (e=3/3 block=0 draw=5 disc=0)

  - hand:
    - [0] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [1] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [2] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [3] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [4] `DefendIronclad` cost=1 canPlay=True target=Self
  - enemies:
    - [0] `FUZZY_WURM_CRAWLER` 20/55 block=0 → Attack 4×1  powers=[STRENGTH_POWER:7]

  → play card [0] target=0
  → play card [0] target=0
  → play card [0] target=0
  → end_turn → round transition
#### Round 4  (e=3/3 block=0 draw=0 disc=5)

  - hand:
    - [0] `DefendIronclad` cost=1 canPlay=True target=Self
    - [1] `DefendIronclad` cost=1 canPlay=True target=Self
    - [2] `DefendIronclad` cost=1 canPlay=True target=Self
    - [3] `Bash` cost=2 canPlay=True target=AnyEnemy
    - [4] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `FUZZY_WURM_CRAWLER` 2/55 block=0 → Attack 4×1  powers=[STRENGTH_POWER:7]

  → play card [0]
  → play card [0]
  → play card [0]
  → end_turn → round transition
#### Round 5  (e=3/3 block=0 draw=5 disc=0)

  - hand:
    - [0] `DefendIronclad` cost=1 canPlay=True target=Self
    - [1] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [2] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [3] `DefendIronclad` cost=1 canPlay=True target=Self
    - [4] `DefendIronclad` cost=1 canPlay=True target=Self
  - enemies:
    - [0] `FUZZY_WURM_CRAWLER` 2/55 block=0 → Buff  powers=[STRENGTH_POWER:7]

  → play card [0]
  → play card [0] target=0
  - rewards offered:
    - [0] gold 12g  canSkip=False
    - [1] potion potion=`POTION.ENERGY_POTION (59793748)`  canSkip=False
    - [2] card cards=[`BodySlam`(cost=1), `Tremble`(cost=1), `SwordBoomerang`(cost=1)]  canSkip=True

  - rewards offered:
    - [0] gold 12g  canSkip=False
    - [1] potion potion=`POTION.ENERGY_POTION (59793748)`  canSkip=False
    - [2] card cards=[`BodySlam`(cost=1), `Tremble`(cost=1), `SwordBoomerang`(cost=1)]  canSkip=True

  → claim reward [0] → hp=71 room=CombatRoom
  - rewards offered:
    - [0] potion potion=`POTION.ENERGY_POTION (59793748)`  canSkip=False
    - [1] card cards=[`BodySlam`(cost=1), `Tremble`(cost=1), `SwordBoomerang`(cost=1)]  canSkip=True

  - rewards offered:
    - [0] potion potion=`POTION.ENERGY_POTION (59793748)`  canSkip=False
    - [1] card cards=[`BodySlam`(cost=1), `Tremble`(cost=1), `SwordBoomerang`(cost=1)]  canSkip=True

  → claim reward [0] → hp=71 room=CombatRoom
  - bag: [0]EnergyPotion/Unknown
  - rewards offered:
    - [0] card cards=[`BodySlam`(cost=1), `Tremble`(cost=1), `SwordBoomerang`(cost=1)]  canSkip=True

  - rewards offered:
    - [0] card cards=[`BodySlam`(cost=1), `Tremble`(cost=1), `SwordBoomerang`(cost=1)]  canSkip=True

## Floor 2: MapRoom  (hp=71)

  - map options: (0,2):Unknown

  → skip reward [0] → hp=71 room=MapRoom
  - combat ended (hp=71)

  - heal → hp=80/80

## Floor 3: EventRoom  (hp=80)

  - event options:
    - [0] `WELLSPRING.pages.INITIAL.options.BOTTLE`
    - [1] `WELLSPRING.pages.INITIAL.options.BATHE`

  → pick map (0,2) → EventRoom floor=3
## Floor 3: MapRoom  (hp=80)

  - map options: (0,3):Monster

  → pick event option [1] → MapRoom hp=80
## Floor 4: CombatRoom  (hp=80)


  → pick map (0,3) → CombatRoom floor=4
### Combat #1 on floor 4

#### Round 1  (e=3/3 block=0 draw=5 disc=0)

  - hand:
    - [0] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [1] `DefendIronclad` cost=1 canPlay=True target=Self
    - [2] `DefendIronclad` cost=1 canPlay=True target=Self
    - [3] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [4] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `SHRINKER_BEETLE` 39/39 block=0 → Unknown

  → play card [0] target=0
  → play card [0]
  → play card [0]
  → end_turn → round transition
#### Round 2  (e=3/3 block=0 draw=0 disc=5)

  - hand:
    - [0] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [1] `DefendIronclad` cost=1 canPlay=True target=Self
    - [2] `Bash` cost=2 canPlay=True target=AnyEnemy
    - [3] `DefendIronclad` cost=1 canPlay=True target=Self
    - [4] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `SHRINKER_BEETLE` 33/39 block=0 → Attack 7×1
  - player powers: SHRINK_POWER:-1

  → play card [0] target=0
  → play card [0]
  → play card [1]
  → end_turn → round transition
#### Round 3  (e=3/3 block=0 draw=5 disc=0)

  - hand:
    - [0] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [1] `DefendIronclad` cost=1 canPlay=True target=Self
    - [2] `DefendIronclad` cost=1 canPlay=True target=Self
    - [3] `DefendIronclad` cost=1 canPlay=True target=Self
    - [4] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `SHRINKER_BEETLE` 29/39 block=0 → Attack 13×1
  - player powers: SHRINK_POWER:-1

  → play card [0] target=0
  → play card [0]
  → play card [0]
  → end_turn → round transition
#### Round 4  (e=3/3 block=0 draw=0 disc=5)

  - hand:
    - [0] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [1] `DefendIronclad` cost=1 canPlay=True target=Self
    - [2] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [3] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [4] `Bash` cost=2 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `SHRINKER_BEETLE` 25/39 block=0 → Attack 7×1
  - player powers: SHRINK_POWER:-1

  → play card [0] target=0
  → play card [0]
  → play card [0] target=0
  → end_turn → round transition
#### Round 5  (e=3/3 block=0 draw=5 disc=0)

  - hand:
    - [0] `DefendIronclad` cost=1 canPlay=True target=Self
    - [1] `DefendIronclad` cost=1 canPlay=True target=Self
    - [2] `DefendIronclad` cost=1 canPlay=True target=Self
    - [3] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [4] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `SHRINKER_BEETLE` 17/39 block=0 → Attack 13×1
  - player powers: SHRINK_POWER:-1

  → play card [0]
  → play card [0]
  → play card [0]
  → end_turn → round transition
#### Round 6  (e=3/3 block=0 draw=0 disc=5)

  - hand:
    - [0] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [1] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [2] `DefendIronclad` cost=1 canPlay=True target=Self
    - [3] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [4] `Bash` cost=2 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `SHRINKER_BEETLE` 17/39 block=0 → Attack 7×1
  - player powers: SHRINK_POWER:-1

  → play card [0] target=0
  → play card [0] target=0
  → play card [0]
  → end_turn → round transition
#### Round 7  (e=3/3 block=0 draw=5 disc=0)

  - hand:
    - [0] `DefendIronclad` cost=1 canPlay=True target=Self
    - [1] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [2] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [3] `Bash` cost=2 canPlay=True target=AnyEnemy
    - [4] `DefendIronclad` cost=1 canPlay=True target=Self
  - enemies:
    - [0] `SHRINKER_BEETLE` 9/39 block=0 → Attack 13×1
  - player powers: SHRINK_POWER:-1

  → play card [0]
  → play card [0] target=0
  → play card [0] target=0
  → end_turn → round transition
#### Round 8  (e=3/3 block=0 draw=0 disc=5)

  - hand:
    - [0] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [1] `DefendIronclad` cost=1 canPlay=True target=Self
    - [2] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [3] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [4] `DefendIronclad` cost=1 canPlay=True target=Self
  - enemies:
    - [0] `SHRINKER_BEETLE` 1/39 block=0 → Attack 7×1
  - player powers: SHRINK_POWER:-1

  → play card [0] target=0
  - rewards offered:
    - [0] gold 10g  canSkip=False
    - [1] card cards=[`Headbutt`(cost=1), `ExpectAFight`(cost=2), `BurningPact`(cost=1)]  canSkip=True

  - rewards offered:
    - [0] gold 10g  canSkip=False
    - [1] card cards=[`Headbutt`(cost=1), `ExpectAFight`(cost=2), `BurningPact`(cost=1)]  canSkip=True

  → claim reward [0] → hp=71 room=CombatRoom
  - rewards offered:
    - [0] card cards=[`Headbutt`(cost=1), `ExpectAFight`(cost=2), `BurningPact`(cost=1)]  canSkip=True

  - rewards offered:
    - [0] card cards=[`Headbutt`(cost=1), `ExpectAFight`(cost=2), `BurningPact`(cost=1)]  canSkip=True

## Floor 4: MapRoom  (hp=71)

  - map options: (0,4):Monster

  → skip reward [0] → hp=71 room=MapRoom
  - combat ended (hp=71)

  - heal → hp=80/80

## Floor 5: CombatRoom  (hp=80)


  → pick map (0,4) → CombatRoom floor=5
### Combat #1 on floor 5

#### Round 1  (e=3/3 block=0 draw=5 disc=0)

  - hand:
    - [0] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [1] `DefendIronclad` cost=1 canPlay=True target=Self
    - [2] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [3] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [4] `DefendIronclad` cost=1 canPlay=True target=Self
  - enemies:
    - [0] `NIBBIT` 44/44 block=0 → Attack 12×1

  → play card [0] target=0
  → play card [0]
  → play card [0] target=0
  → end_turn → round transition
#### Round 2  (e=3/3 block=0 draw=0 disc=5)

  - hand:
    - [0] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [1] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [2] `DefendIronclad` cost=1 canPlay=True target=Self
    - [3] `Bash` cost=2 canPlay=True target=AnyEnemy
    - [4] `DefendIronclad` cost=1 canPlay=True target=Self
  - enemies:
    - [0] `NIBBIT` 32/44 block=0 → Attack 6×1 + Defend

  → play card [0] target=0
  → play card [0] target=0
  → play card [0]
  → end_turn → round transition
#### Round 3  (e=3/3 block=0 draw=5 disc=0)

  - hand:
    - [0] `DefendIronclad` cost=1 canPlay=True target=Self
    - [1] `DefendIronclad` cost=1 canPlay=True target=Self
    - [2] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [3] `DefendIronclad` cost=1 canPlay=True target=Self
    - [4] `DefendIronclad` cost=1 canPlay=True target=Self
  - enemies:
    - [0] `NIBBIT` 20/44 block=5 → Buff

  → play card [0]
  → play card [0]
  → play card [0] target=0
  → end_turn → round transition
#### Round 4  (e=3/3 block=0 draw=0 disc=5)

  - hand:
    - [0] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [1] `Bash` cost=2 canPlay=True target=AnyEnemy
    - [2] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [3] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [4] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `NIBBIT` 19/44 block=0 → Attack 12×1  powers=[STRENGTH_POWER:2]

  → play card [0] target=0
  → play card [0] target=0
  → end_turn → round transition
#### Round 5  (e=3/3 block=0 draw=5 disc=0)

  - hand:
    - [0] `DefendIronclad` cost=1 canPlay=True target=Self
    - [1] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [2] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [3] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [4] `DefendIronclad` cost=1 canPlay=True target=Self
  - enemies:
    - [0] `NIBBIT` 5/44 block=0 → Attack 6×1 + Defend  powers=[STRENGTH_POWER:2,VULNERABLE_POWER:1]

  → play card [0]
  → play card [0] target=0
  - rewards offered:
    - [0] gold 10g  canSkip=False
    - [1] card cards=[`Bully`(cost=0), `Thunderclap`(cost=1), `Bludgeon`(cost=3)]  canSkip=True

  - rewards offered:
    - [0] gold 10g  canSkip=False
    - [1] card cards=[`Bully`(cost=0), `Thunderclap`(cost=1), `Bludgeon`(cost=3)]  canSkip=True

  → claim reward [0] → hp=64 room=CombatRoom
  - rewards offered:
    - [0] card cards=[`Bully`(cost=0), `Thunderclap`(cost=1), `Bludgeon`(cost=3)]  canSkip=True

  - rewards offered:
    - [0] card cards=[`Bully`(cost=0), `Thunderclap`(cost=1), `Bludgeon`(cost=3)]  canSkip=True

## Floor 5: MapRoom  (hp=64)

  - map options: (0,5):Unknown

  → skip reward [0] → hp=64 room=MapRoom
  - combat ended (hp=64)

  - heal → hp=80/80

## Floor 6: EventRoom  (hp=80)

  - event options:
    - [0] `THE_LEGENDS_WERE_TRUE.pages.INITIAL.options.NAB_THE_MAP`
    - [1] `THE_LEGENDS_WERE_TRUE.pages.INITIAL.options.SLOWLY_FIND_AN_EXIT`

  → pick map (0,5) → EventRoom floor=6
## Floor 6: MapRoom  (hp=72)

  - map options: (0,6):RestSite

  → pick event option [1] → MapRoom hp=72
  - bag: [0]EnergyPotion/Unknown,[1]RegenPotion/Unknown
  - heal → hp=80/80

## Floor 7: RestSiteRoom  (hp=80)


  → pick map (0,6) → RestSiteRoom floor=7
## Floor 7: MapRoom  (hp=80)

  - map options: (0,7):Elite

  → pick rest option [0] → MapRoom hp=80
## Floor 8: CombatRoom  (hp=80)


  → pick map (0,7) → CombatRoom floor=8
### Combat #1 on floor 8

#### Round 1  (e=3/3 block=0 draw=5 disc=0)

  - hand:
    - [0] `DefendIronclad` cost=1 canPlay=True target=Self
    - [1] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [2] `DefendIronclad` cost=1 canPlay=True target=Self
    - [3] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [4] `DefendIronclad` cost=1 canPlay=True target=Self
  - enemies:
    - [0] `PHROG_PARASITE` 64/64 block=0 → Unknown  powers=[INFESTED_POWER:4]

  → play card [0]
  → play card [0] target=0
  → play card [0]
  → end_turn → round transition
#### Round 2  (e=3/3 block=0 draw=0 disc=8)

  - hand:
    - [0] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [1] `Bash` cost=2 canPlay=True target=AnyEnemy
    - [2] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [3] `DefendIronclad` cost=1 canPlay=True target=Self
    - [4] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `PHROG_PARASITE` 58/64 block=0 → Attack 4×4  powers=[INFESTED_POWER:4]

  → play card [0] target=0
  → play card [0] target=0
  → end_turn → round transition
#### Round 3  (e=3/3 block=0 draw=8 disc=0)

  - hand:
    - [0] `DefendIronclad` cost=1 canPlay=True target=Self
    - [1] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [2] `Infection` cost=0 canPlay=False target=None
    - [3] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [4] `Infection` cost=0 canPlay=False target=None
  - enemies:
    - [0] `PHROG_PARASITE` 44/64 block=0 → Unknown  powers=[INFESTED_POWER:4,VULNERABLE_POWER:1]

  → play card [0]
  → play card [0] target=0
  → play card [1] target=0
  → end_turn → round transition
#### Round 4  (e=3/3 block=0 draw=3 disc=8)

  - hand:
    - [0] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [1] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [2] `DefendIronclad` cost=1 canPlay=True target=Self
    - [3] `DefendIronclad` cost=1 canPlay=True target=Self
    - [4] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `PHROG_PARASITE` 26/64 block=0 → Attack 4×4  powers=[INFESTED_POWER:4]

  → play card [0] target=0
  → play card [0] target=0
  → play card [0]
  → end_turn → round transition
#### Round 5  (e=3/3 block=0 draw=11 disc=0)

  - hand:
    - [0] `DefendIronclad` cost=1 canPlay=True target=Self
    - [1] `Infection` cost=0 canPlay=False target=None
    - [2] `Bash` cost=2 canPlay=True target=AnyEnemy
    - [3] `Infection` cost=0 canPlay=False target=None
    - [4] `Infection` cost=0 canPlay=False target=None
  - enemies:
    - [0] `PHROG_PARASITE` 14/64 block=0 → Unknown  powers=[INFESTED_POWER:4]

  → play card [0]
  → play card [1] target=0
  → end_turn → round transition
#### Round 6  (e=3/3 block=0 draw=6 disc=8)

  - hand:
    - [0] `Infection` cost=0 canPlay=False target=None
    - [1] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [2] `DefendIronclad` cost=1 canPlay=True target=Self
    - [3] `Infection` cost=0 canPlay=False target=None
    - [4] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `PHROG_PARASITE` 6/64 block=0 → Attack 4×4  powers=[INFESTED_POWER:4,VULNERABLE_POWER:1]

  → play card [1] target=0
  → play card [1]
  → play card [2] target=0
  → end_turn → round transition
#### Round 7  (e=3/3 block=0 draw=1 disc=13)

  - hand:
    - [0] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [1] `Infection` cost=0 canPlay=False target=None
    - [2] `DefendIronclad` cost=1 canPlay=True target=Self
    - [3] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [4] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `WRIGGLER` 13/19 block=0 → Attack 6×1
    - [1] `WRIGGLER` 21/21 block=0 → Buff + Unknown
    - [2] `WRIGGLER` 18/18 block=0 → Attack 6×1
    - [3] `WRIGGLER` 17/17 block=0 → Buff + Unknown

  → play card [0] target=0
  → play card [1]
  → play card [1] target=0
  → end_turn → round transition
#### Round 8  (e=3/3 block=0 draw=16 disc=0)

  - hand:
    - [0] `DefendIronclad` cost=1 canPlay=True target=Self
    - [1] `Infection` cost=0 canPlay=False target=None
    - [2] `Infection` cost=0 canPlay=False target=None
    - [3] `Infection` cost=0 canPlay=False target=None
    - [4] `DefendIronclad` cost=1 canPlay=True target=Self
  - enemies:
    - [0] `WRIGGLER` 1/19 block=0 → Buff + Unknown
    - [1] `WRIGGLER` 21/21 block=0 → Attack 6×1  powers=[STRENGTH_POWER:2]
    - [2] `WRIGGLER` 18/18 block=0 → Buff + Unknown
    - [3] `WRIGGLER` 17/17 block=0 → Attack 6×1  powers=[STRENGTH_POWER:2]

  → play card [0]
  → play card [3]
  → end_turn → round transition
#### Round 9  (e=3/3 block=0 draw=11 disc=7)

  - hand:
    - [0] `Infection` cost=0 canPlay=False target=None
    - [1] `Infection` cost=0 canPlay=False target=None
    - [2] `DefendIronclad` cost=1 canPlay=True target=Self
    - [3] `Infection` cost=0 canPlay=False target=None
    - [4] `Infection` cost=0 canPlay=False target=None
  - enemies:
    - [0] `WRIGGLER` 1/19 block=0 → Attack 6×1  powers=[STRENGTH_POWER:2]
    - [1] `WRIGGLER` 21/21 block=0 → Buff + Unknown  powers=[STRENGTH_POWER:2]
    - [2] `WRIGGLER` 18/18 block=0 → Attack 6×1  powers=[STRENGTH_POWER:2]
    - [3] `WRIGGLER` 17/17 block=0 → Buff + Unknown  powers=[STRENGTH_POWER:2]

  → play card [2]
  → end_turn → round transition
  - rewards offered:
    - [0] gold 41g  canSkip=False
    - [1] relic relic=``  canSkip=False
    - [2] card cards=[`Dismantle`(cost=1), `Cascade`(cost=0), `Thunderclap`(cost=1)]  canSkip=True

  - rewards offered:
    - [0] gold 41g  canSkip=False
    - [1] relic relic=``  canSkip=False
    - [2] card cards=[`Dismantle`(cost=1), `Cascade`(cost=0), `Thunderclap`(cost=1)]  canSkip=True

  - heal → hp=80/80

  - rewards offered:
    - [0] gold 41g  canSkip=False
    - [1] relic relic=``  canSkip=False
    - [2] card cards=[`Dismantle`(cost=1), `Cascade`(cost=0), `Thunderclap`(cost=1)]  canSkip=True

  → claim reward [0] → hp=80 room=CombatRoom
  - rewards offered:
    - [0] relic relic=``  canSkip=False
    - [1] card cards=[`Dismantle`(cost=1), `Cascade`(cost=0), `Thunderclap`(cost=1)]  canSkip=True

  - rewards offered:
    - [0] relic relic=``  canSkip=False
    - [1] card cards=[`Dismantle`(cost=1), `Cascade`(cost=0), `Thunderclap`(cost=1)]  canSkip=True

  → claim reward [0] → hp=80 room=CombatRoom
  - rewards offered:
    - [0] card cards=[`Dismantle`(cost=1), `Cascade`(cost=0), `Thunderclap`(cost=1)]  canSkip=True

  - rewards offered:
    - [0] card cards=[`Dismantle`(cost=1), `Cascade`(cost=0), `Thunderclap`(cost=1)]  canSkip=True

## Floor 8: MapRoom  (hp=80)

  - map options: (0,8):Monster

  → skip reward [0] → hp=80 room=MapRoom
  - combat ended (hp=80)

## Floor 9: CombatRoom  (hp=80)


  → pick map (0,8) → CombatRoom floor=9
### Combat #1 on floor 9

#### Round 1  (e=3/3 block=0 draw=5 disc=0)

  - hand:
    - [0] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [1] `DefendIronclad` cost=1 canPlay=True target=Self
    - [2] `DefendIronclad` cost=1 canPlay=True target=Self
    - [3] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [4] `DefendIronclad` cost=1 canPlay=True target=Self
  - enemies:
    - [0] `MAWLER` 72/72 block=0 → Attack 4×2

  → play card [0] target=0
  → play card [0]
  → play card [0]
  → end_turn → round transition
#### Round 2  (e=3/3 block=0 draw=0 disc=5)

  - hand:
    - [0] `Bash` cost=2 canPlay=True target=AnyEnemy
    - [1] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [2] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [3] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [4] `DefendIronclad` cost=1 canPlay=True target=Self
  - enemies:
    - [0] `MAWLER` 66/72 block=0 → Attack 14×1

  → play card [0] target=0
  → play card [0] target=0
  → end_turn → round transition
#### Round 3  (e=3/3 block=0 draw=5 disc=0)

  - hand:
    - [0] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [1] `DefendIronclad` cost=1 canPlay=True target=Self
    - [2] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [3] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [4] `DefendIronclad` cost=1 canPlay=True target=Self
  - enemies:
    - [0] `MAWLER` 49/72 block=0 → Debuff  powers=[VULNERABLE_POWER:1]

  → play card [0] target=0
  → play card [0]
  → play card [0] target=0
  → end_turn → round transition
#### Round 4  (e=3/3 block=0 draw=0 disc=5)

  - hand:
    - [0] `DefendIronclad` cost=1 canPlay=True target=Self
    - [1] `Bash` cost=2 canPlay=True target=AnyEnemy
    - [2] `DefendIronclad` cost=1 canPlay=True target=Self
    - [3] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [4] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `MAWLER` 31/72 block=0 → Attack 4×2
  - player powers: VULNERABLE_POWER:3

  → play card [0]
  → play card [0] target=0
  → end_turn → round transition
#### Round 5  (e=3/3 block=0 draw=5 disc=0)

  - hand:
    - [0] `DefendIronclad` cost=1 canPlay=True target=Self
    - [1] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [2] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [3] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [4] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `MAWLER` 23/72 block=0 → Attack 14×1  powers=[VULNERABLE_POWER:1]
  - player powers: VULNERABLE_POWER:3

  → play card [0]
  → play card [0] target=0
  → play card [0] target=0
  → end_turn → round transition
#### Round 6  (e=3/3 block=0 draw=0 disc=5)

  - hand:
    - [0] `DefendIronclad` cost=1 canPlay=True target=Self
    - [1] `Bash` cost=2 canPlay=True target=AnyEnemy
    - [2] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [3] `DefendIronclad` cost=1 canPlay=True target=Self
    - [4] `DefendIronclad` cost=1 canPlay=True target=Self
  - enemies:
    - [0] `MAWLER` 5/72 block=0 → Attack 4×2
  - player powers: VULNERABLE_POWER:3

  → play card [0]
  → play card [0] target=0
  - rewards offered:
    - [0] gold 18g  canSkip=False
    - [1] potion potion=`POTION.ENTROPIC_BREW (37132456)`  canSkip=False
    - [2] card cards=[`Uppercut`(cost=2), `Armaments`(cost=1), `StoneArmor`(cost=1)]  canSkip=True

  - rewards offered:
    - [0] gold 18g  canSkip=False
    - [1] potion potion=`POTION.ENTROPIC_BREW (37132456)`  canSkip=False
    - [2] card cards=[`Uppercut`(cost=2), `Armaments`(cost=1), `StoneArmor`(cost=1)]  canSkip=True

  → claim reward [0] → hp=54 room=CombatRoom
  - rewards offered:
    - [0] potion potion=`POTION.ENTROPIC_BREW (37132456)`  canSkip=False
    - [1] card cards=[`Uppercut`(cost=2), `Armaments`(cost=1), `StoneArmor`(cost=1)]  canSkip=True

  - rewards offered:
    - [0] potion potion=`POTION.ENTROPIC_BREW (37132456)`  canSkip=False
    - [1] card cards=[`Uppercut`(cost=2), `Armaments`(cost=1), `StoneArmor`(cost=1)]  canSkip=True

  → claim reward [0] → hp=54 room=CombatRoom
  - bag: [0]EnergyPotion/Unknown,[1]RegenPotion/Unknown,[2]EntropicBrew/Self
  - rewards offered:
    - [0] card cards=[`Uppercut`(cost=2), `Armaments`(cost=1), `StoneArmor`(cost=1)]  canSkip=True

  - rewards offered:
    - [0] card cards=[`Uppercut`(cost=2), `Armaments`(cost=1), `StoneArmor`(cost=1)]  canSkip=True

## Floor 9: MapRoom  (hp=54)

  - map options: (0,9):Treasure

  → skip reward [0] → hp=54 room=MapRoom
  - combat ended (hp=54)

  - heal → hp=80/80

## Floor 10: TreasureRoom  (hp=80)


  → pick map (0,9) → TreasureRoom floor=10
## Floor 10: MapRoom  (hp=80)

  - map options: (0,10):RestSite

  → leave treasure → MapRoom relics=`BURNING_BLOOD`, `ODDLY_SMOOTH_STONE`, `GORGET`
## Floor 11: RestSiteRoom  (hp=80)


  → pick map (0,10) → RestSiteRoom floor=11
## Floor 11: MapRoom  (hp=80)

  - map options: (0,11):Unknown

  → pick rest option [0] → MapRoom hp=80
## Floor 12: CombatRoom  (hp=80)


  → pick map (0,11) → CombatRoom floor=12
### Combat #1 on floor 12

#### Round 1  (e=3/3 block=0 draw=5 disc=0)

  - hand:
    - [0] `DefendIronclad` cost=1 canPlay=True target=Self
    - [1] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [2] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [3] `DefendIronclad` cost=1 canPlay=True target=Self
    - [4] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `LEAF_SLIME_S` 11/11 block=0 → Attack 3×1
    - [1] `TWIG_SLIME_S` 9/9 block=0 → Attack 4×1
    - [2] `SLITHERING_STRANGLER` 55/55 block=0 → Debuff

  → play card [0]
  → play card [0] target=0
  → play card [0] target=0
  → end_turn → round transition
#### Round 2  (e=3/3 block=0 draw=0 disc=5)

  - hand:
    - [0] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [1] `Bash` cost=2 canPlay=True target=AnyEnemy
    - [2] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [3] `DefendIronclad` cost=1 canPlay=True target=Self
    - [4] `DefendIronclad` cost=1 canPlay=True target=Self
  - enemies:
    - [0] `TWIG_SLIME_S` 9/9 block=0 → Attack 4×1
    - [1] `SLITHERING_STRANGLER` 55/55 block=0 → Attack 7×1 + Defend
  - player powers: CONSTRICT_POWER:3

  → play card [0] target=0
  → play card [0] target=0
  → end_turn → round transition
#### Round 3  (e=3/3 block=0 draw=5 disc=0)

  - hand:
    - [0] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [1] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [2] `DefendIronclad` cost=1 canPlay=True target=Self
    - [3] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [4] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `SLITHERING_STRANGLER` 55/55 block=5 → Debuff
  - player powers: CONSTRICT_POWER:3

  → play card [0] target=0
  → play card [0] target=0
  → play card [0]
  → end_turn → round transition
#### Round 4  (e=3/3 block=0 draw=0 disc=5)

  - hand:
    - [0] `DefendIronclad` cost=1 canPlay=True target=Self
    - [1] `Bash` cost=2 canPlay=True target=AnyEnemy
    - [2] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [3] `DefendIronclad` cost=1 canPlay=True target=Self
    - [4] `DefendIronclad` cost=1 canPlay=True target=Self
  - enemies:
    - [0] `SLITHERING_STRANGLER` 48/55 block=0 → Attack 7×1 + Defend
  - player powers: CONSTRICT_POWER:6

  → play card [0]
  → play card [0] target=0
  → end_turn → round transition
#### Round 5  (e=3/3 block=0 draw=5 disc=0)

  - hand:
    - [0] `DefendIronclad` cost=1 canPlay=True target=Self
    - [1] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [2] `DefendIronclad` cost=1 canPlay=True target=Self
    - [3] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [4] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `SLITHERING_STRANGLER` 40/55 block=5 → Debuff  powers=[VULNERABLE_POWER:1]
  - player powers: CONSTRICT_POWER:6

  → play card [0]
  → play card [0] target=0
  → play card [0]
  → end_turn → round transition
#### Round 6  (e=3/3 block=0 draw=0 disc=5)

  - hand:
    - [0] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [1] `DefendIronclad` cost=1 canPlay=True target=Self
    - [2] `Bash` cost=2 canPlay=True target=AnyEnemy
    - [3] `DefendIronclad` cost=1 canPlay=True target=Self
    - [4] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `SLITHERING_STRANGLER` 36/55 block=0 → Attack 7×1 + Defend
  - player powers: CONSTRICT_POWER:9

  → play card [0] target=0
  → play card [0]
  → play card [1]
  → end_turn → round transition
#### Round 7  (e=3/3 block=0 draw=5 disc=0)

  - hand:
    - [0] `DefendIronclad` cost=1 canPlay=True target=Self
    - [1] `DefendIronclad` cost=1 canPlay=True target=Self
    - [2] `DefendIronclad` cost=1 canPlay=True target=Self
    - [3] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [4] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `SLITHERING_STRANGLER` 30/55 block=5 → Debuff
  - player powers: CONSTRICT_POWER:9

  → play card [0]
  → play card [0]
  → play card [0]
  → end_turn → round transition
#### Round 8  (e=3/3 block=0 draw=0 disc=5)

  - hand:
    - [0] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [1] `DefendIronclad` cost=1 canPlay=True target=Self
    - [2] `Bash` cost=2 canPlay=True target=AnyEnemy
    - [3] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [4] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `SLITHERING_STRANGLER` 30/55 block=0 → Attack 12×1
  - player powers: CONSTRICT_POWER:12

  → play card [0] target=0
  → play card [0]
  → play card [1] target=0
  → end_turn → round transition
#### Round 9  (e=3/3 block=0 draw=5 disc=0)

  - hand:
    - [0] `DefendIronclad` cost=1 canPlay=True target=Self
    - [1] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [2] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [3] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [4] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `SLITHERING_STRANGLER` 18/55 block=0 → Debuff
  - player powers: CONSTRICT_POWER:12

  → play card [0]
  → play card [0] target=0
  → play card [0] target=0
  → end_turn → round transition
#### Round 10  (e=3/3 block=0 draw=0 disc=5)

  - hand:
    - [0] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [1] `DefendIronclad` cost=1 canPlay=True target=Self
    - [2] `DefendIronclad` cost=1 canPlay=True target=Self
    - [3] `DefendIronclad` cost=1 canPlay=True target=Self
    - [4] `Bash` cost=2 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `SLITHERING_STRANGLER` 6/55 block=0 → Attack 12×1
  - player powers: CONSTRICT_POWER:15

  → play card [0] target=0
  - rewards offered:
    - [0] gold 13g  canSkip=False
    - [1] potion potion=`POTION.COLORLESS_POTION (32595925)`  canSkip=False
    - [2] card cards=[`TrueGrit`(cost=1), `SecondWind`(cost=1), `Armaments`(cost=1)]  canSkip=True

  - rewards offered:
    - [0] gold 13g  canSkip=False
    - [1] potion potion=`POTION.COLORLESS_POTION (32595925)`  canSkip=False
    - [2] card cards=[`TrueGrit`(cost=1), `SecondWind`(cost=1), `Armaments`(cost=1)]  canSkip=True

  → claim reward [0] → hp=64 room=CombatRoom
  - rewards offered:
    - [0] potion potion=`POTION.COLORLESS_POTION (32595925)`  canSkip=False
    - [1] card cards=[`TrueGrit`(cost=1), `SecondWind`(cost=1), `Armaments`(cost=1)]  canSkip=True

  - rewards offered:
    - [0] potion potion=`POTION.COLORLESS_POTION (32595925)`  canSkip=False
    - [1] card cards=[`TrueGrit`(cost=1), `SecondWind`(cost=1), `Armaments`(cost=1)]  canSkip=True

  → claim reward [0] → hp=64 room=CombatRoom
  - rewards offered:
    - [0] card cards=[`TrueGrit`(cost=1), `SecondWind`(cost=1), `Armaments`(cost=1)]  canSkip=True

  - rewards offered:
    - [0] card cards=[`TrueGrit`(cost=1), `SecondWind`(cost=1), `Armaments`(cost=1)]  canSkip=True

## Floor 12: MapRoom  (hp=64)

  - map options: (0,12):RestSite

  → skip reward [0] → hp=64 room=MapRoom
  - combat ended (hp=64)

  - heal → hp=80/80

## Floor 13: RestSiteRoom  (hp=80)


  → pick map (0,12) → RestSiteRoom floor=13
## Floor 13: MapRoom  (hp=80)

  - map options: (0,13):Unknown

  → pick rest option [0] → MapRoom hp=80
## Floor 14: EventRoom  (hp=80)

  - event options:
    - [0] `JUNGLE_MAZE_ADVENTURE.pages.INITIAL.options.SOLO_QUEST`
    - [1] `JUNGLE_MAZE_ADVENTURE.pages.INITIAL.options.JOIN_FORCES`

  → pick map (0,13) → EventRoom floor=14
## Floor 14: MapRoom  (hp=80)

  - map options: (0,14):Monster

  → pick event option [1] → MapRoom hp=80
## Floor 15: CombatRoom  (hp=80)


  → pick map (0,14) → CombatRoom floor=15
### Combat #1 on floor 15

#### Round 1  (e=3/3 block=0 draw=5 disc=0)

  - hand:
    - [0] `DefendIronclad` cost=1 canPlay=True target=Self
    - [1] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [2] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [3] `DefendIronclad` cost=1 canPlay=True target=Self
    - [4] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `FOGMOG` 74/74 block=0 → Unknown

  → play card [0]
  → play card [0] target=0
  → play card [0] target=0
  → end_turn → round transition
#### Round 2  (e=3/3 block=0 draw=0 disc=5)

  - hand:
    - [0] `DefendIronclad` cost=1 canPlay=True target=Self
    - [1] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [2] `DefendIronclad` cost=1 canPlay=True target=Self
    - [3] `Bash` cost=2 canPlay=True target=AnyEnemy
    - [4] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `EYE_WITH_TEETH` 6/6 block=0 → Unknown  powers=[ILLUSION_POWER:1,MINION_POWER:1]
    - [1] `FOGMOG` 62/74 block=0 → Attack 8×1 + Buff

  → play card [0]
  → play card [0] target=0
  → play card [0]
  → end_turn → round transition
#### Round 3  (e=3/3 block=0 draw=5 disc=0)

  - hand:
    - [0] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [1] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [2] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [3] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [4] `Bash` cost=2 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `EYE_WITH_TEETH` 6/6 block=0 → Unknown  powers=[ILLUSION_POWER:1,MINION_POWER:1]
    - [1] `FOGMOG` 62/74 block=0 → Attack 8×1 + Buff  powers=[STRENGTH_POWER:1]

  → play card [0] target=0
  → play card [0] target=0
  → play card [0] target=0
  → end_turn → round transition
#### Round 4  (e=3/3 block=0 draw=0 disc=5)

  - hand:
    - [0] `DefendIronclad` cost=1 canPlay=True target=Self
    - [1] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [2] `DefendIronclad` cost=1 canPlay=True target=Self
    - [3] `DefendIronclad` cost=1 canPlay=True target=Self
    - [4] `DefendIronclad` cost=1 canPlay=True target=Self
  - enemies:
    - [0] `EYE_WITH_TEETH` 6/6 block=0 → Unknown  powers=[ILLUSION_POWER:1,MINION_POWER:1]
    - [1] `FOGMOG` 50/74 block=0 → Attack 14×1  powers=[STRENGTH_POWER:2]

  → play card [0]
  → play card [0] target=0
  → play card [0]
  → end_turn → round transition
#### Round 5  (e=3/3 block=0 draw=5 disc=0)

  - hand:
    - [0] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [1] `DefendIronclad` cost=1 canPlay=True target=Self
    - [2] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [3] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [4] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `EYE_WITH_TEETH` 6/6 block=0 → Unknown  powers=[ILLUSION_POWER:1,MINION_POWER:1]
    - [1] `FOGMOG` 50/74 block=0 → Attack 8×1 + Buff  powers=[STRENGTH_POWER:2]

  → play card [0] target=0
  → play card [0]
  → play card [0] target=0
  → end_turn → round transition
#### Round 6  (e=3/3 block=0 draw=0 disc=5)

  - hand:
    - [0] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [1] `DefendIronclad` cost=1 canPlay=True target=Self
    - [2] `DefendIronclad` cost=1 canPlay=True target=Self
    - [3] `DefendIronclad` cost=1 canPlay=True target=Self
    - [4] `Bash` cost=2 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `EYE_WITH_TEETH` 6/6 block=0 → Unknown  powers=[ILLUSION_POWER:1,MINION_POWER:1]
    - [1] `FOGMOG` 44/74 block=0 → Attack 14×1  powers=[STRENGTH_POWER:3]

  → play card [0] target=0
  → play card [0]
  → play card [0]
  → end_turn → round transition
#### Round 7  (e=3/3 block=0 draw=5 disc=0)

  - hand:
    - [0] `DefendIronclad` cost=1 canPlay=True target=Self
    - [1] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [2] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [3] `Bash` cost=2 canPlay=True target=AnyEnemy
    - [4] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `EYE_WITH_TEETH` 6/6 block=0 → Unknown  powers=[ILLUSION_POWER:1,MINION_POWER:1]
    - [1] `FOGMOG` 44/74 block=0 → Attack 8×1 + Buff  powers=[STRENGTH_POWER:3]

  → play card [0]
  → play card [0] target=0
  → play card [0] target=0
  → end_turn → round transition
#### Round 8  (e=3/3 block=0 draw=0 disc=5)

  - hand:
    - [0] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [1] `DefendIronclad` cost=1 canPlay=True target=Self
    - [2] `DefendIronclad` cost=1 canPlay=True target=Self
    - [3] `DefendIronclad` cost=1 canPlay=True target=Self
    - [4] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `EYE_WITH_TEETH` 6/6 block=0 → Unknown  powers=[ILLUSION_POWER:1,MINION_POWER:1]
    - [1] `FOGMOG` 38/74 block=0 → Attack 14×1  powers=[STRENGTH_POWER:4]

  → play card [0] target=0
  → play card [0]
  → play card [0]
  → end_turn → round transition
#### Round 9  (e=3/3 block=0 draw=5 disc=0)

  - hand:
    - [0] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [1] `DefendIronclad` cost=1 canPlay=True target=Self
    - [2] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [3] `DefendIronclad` cost=1 canPlay=True target=Self
    - [4] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `EYE_WITH_TEETH` 6/6 block=0 → Unknown  powers=[ILLUSION_POWER:1,MINION_POWER:1]
    - [1] `FOGMOG` 38/74 block=0 → Attack 8×1 + Buff  powers=[STRENGTH_POWER:4]

  → play card [0] target=0
  → play card [0]
  → play card [0] target=0
  → end_turn → round transition
#### Round 10  (e=3/3 block=0 draw=0 disc=5)

  - hand:
    - [0] `Bash` cost=2 canPlay=True target=AnyEnemy
    - [1] `DefendIronclad` cost=1 canPlay=True target=Self
    - [2] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [3] `DefendIronclad` cost=1 canPlay=True target=Self
    - [4] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `EYE_WITH_TEETH` 6/6 block=0 → Unknown  powers=[ILLUSION_POWER:1,MINION_POWER:1]
    - [1] `FOGMOG` 32/74 block=0 → Attack 14×1  powers=[STRENGTH_POWER:5]

  → play card [0] target=0
  → play card [0]
  → end_turn → round transition
#### Round 11  (e=3/3 block=0 draw=5 disc=0)

  - hand:
    - [0] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [1] `DefendIronclad` cost=1 canPlay=True target=Self
    - [2] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [3] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [4] `DefendIronclad` cost=1 canPlay=True target=Self
  - enemies:
    - [0] `EYE_WITH_TEETH` 6/6 block=0 → Unknown  powers=[ILLUSION_POWER:1,MINION_POWER:1]
    - [1] `FOGMOG` 32/74 block=0 → Attack 8×1 + Buff  powers=[STRENGTH_POWER:5]

  → play card [0] target=0
  → play card [0]
  → play card [0] target=0
  → end_turn → round transition
#### Round 12  (e=3/3 block=0 draw=0 disc=5)

  - hand:
    - [0] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [1] `DefendIronclad` cost=1 canPlay=True target=Self
    - [2] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [3] `Bash` cost=2 canPlay=True target=AnyEnemy
    - [4] `DefendIronclad` cost=1 canPlay=True target=Self
  - enemies:
    - [0] `EYE_WITH_TEETH` 6/6 block=0 → Unknown  powers=[ILLUSION_POWER:1,MINION_POWER:1]
    - [1] `FOGMOG` 26/74 block=0 → Attack 14×1  powers=[STRENGTH_POWER:6]

  → play card [0] target=0
  → play card [0]
  → play card [0] target=0
  → end_turn → round transition
  - rewards offered:
    - [0] gold 11g  canSkip=False
    - [1] potion potion=`POTION.BLOCK_POTION (19884118)`  canSkip=False
    - [2] card cards=[`Taunt`(cost=1), `Armaments`(cost=1), `BloodWall`(cost=2)]  canSkip=True

  - rewards offered:
    - [0] gold 11g  canSkip=False
    - [1] potion potion=`POTION.BLOCK_POTION (19884118)`  canSkip=False
    - [2] card cards=[`Taunt`(cost=1), `Armaments`(cost=1), `BloodWall`(cost=2)]  canSkip=True

  - heal → hp=80/80

  - rewards offered:
    - [0] gold 11g  canSkip=False
    - [1] potion potion=`POTION.BLOCK_POTION (19884118)`  canSkip=False
    - [2] card cards=[`Taunt`(cost=1), `Armaments`(cost=1), `BloodWall`(cost=2)]  canSkip=True

  → claim reward [0] → hp=80 room=CombatRoom
  - rewards offered:
    - [0] potion potion=`POTION.BLOCK_POTION (19884118)`  canSkip=False
    - [1] card cards=[`Taunt`(cost=1), `Armaments`(cost=1), `BloodWall`(cost=2)]  canSkip=True

  - rewards offered:
    - [0] potion potion=`POTION.BLOCK_POTION (19884118)`  canSkip=False
    - [1] card cards=[`Taunt`(cost=1), `Armaments`(cost=1), `BloodWall`(cost=2)]  canSkip=True

  → claim reward [0] → hp=80 room=CombatRoom
  - rewards offered:
    - [0] card cards=[`Taunt`(cost=1), `Armaments`(cost=1), `BloodWall`(cost=2)]  canSkip=True

  - rewards offered:
    - [0] card cards=[`Taunt`(cost=1), `Armaments`(cost=1), `BloodWall`(cost=2)]  canSkip=True

## Floor 15: MapRoom  (hp=80)

  - map options: (0,15):RestSite

  → skip reward [0] → hp=80 room=MapRoom
  - combat ended (hp=80)

## Floor 16: RestSiteRoom  (hp=80)


  → pick map (0,15) → RestSiteRoom floor=16
## Floor 16: MapRoom  (hp=80)

  - map options: (3,16):Boss

  → pick rest option [0] → MapRoom hp=80
## Floor 17: BossRoom  (hp=80)


  → pick map (3,16) → BossRoom floor=17
### Combat #1 on floor 17

#### Round 1  (e=3/3 block=0 draw=5 disc=0)

  - hand:
    - [0] `DefendIronclad` cost=1 canPlay=True target=Self
    - [1] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [2] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [3] `DefendIronclad` cost=1 canPlay=True target=Self
    - [4] `Bash` cost=2 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `VANTOM` 173/173 block=0 → Attack 7×1  powers=[SLIPPERY_POWER:9]

  - heal → hp=80/80

  → play card [0]
  → play card [0] target=0
  → play card [0] target=0
  → end_turn → round transition
#### Round 2  (e=3/3 block=0 draw=0 disc=5)

  - hand:
    - [0] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [1] `DefendIronclad` cost=1 canPlay=True target=Self
    - [2] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [3] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [4] `DefendIronclad` cost=1 canPlay=True target=Self
  - enemies:
    - [0] `VANTOM` 171/173 block=0 → Attack 6×2  powers=[SLIPPERY_POWER:7]

  → play card [0] target=0
  → play card [0]
  → play card [0] target=0
  → end_turn → round transition
#### Round 3  (e=3/3 block=0 draw=5 disc=0)

  - hand:
    - [0] `DefendIronclad` cost=1 canPlay=True target=Self
    - [1] `DefendIronclad` cost=1 canPlay=True target=Self
    - [2] `DefendIronclad` cost=1 canPlay=True target=Self
    - [3] `Bash` cost=2 canPlay=True target=AnyEnemy
    - [4] `DefendIronclad` cost=1 canPlay=True target=Self
  - enemies:
    - [0] `VANTOM` 169/173 block=0 → Attack 27×1 + Unknown  powers=[SLIPPERY_POWER:5]

  → play card [0]
  → play card [0]
  → play card [0]
  → end_turn → round transition
#### Round 4  (e=3/3 block=0 draw=0 disc=5)

  - hand:
    - [0] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [1] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [2] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [3] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [4] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `VANTOM` 169/173 block=0 → Buff  powers=[SLIPPERY_POWER:5]

  → play card [0] target=0
  → play card [0] target=0
  → play card [0] target=0
  → end_turn → round transition
#### Round 5  (e=3/3 block=0 draw=5 disc=0)

  - hand:
    - [0] `DefendIronclad` cost=1 canPlay=True target=Self
    - [1] `DefendIronclad` cost=1 canPlay=True target=Self
    - [2] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [3] `DefendIronclad` cost=1 canPlay=True target=Self
    - [4] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `VANTOM` 166/173 block=0 → Attack 7×1  powers=[SLIPPERY_POWER:2,STRENGTH_POWER:2]

  → play card [0]
  → play card [0]
  → play card [0] target=0
  → end_turn → round transition
#### Round 6  (e=3/3 block=0 draw=0 disc=5)

  - hand:
    - [0] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [1] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [2] `Bash` cost=2 canPlay=True target=AnyEnemy
    - [3] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [4] `DefendIronclad` cost=1 canPlay=True target=Self
  - enemies:
    - [0] `VANTOM` 165/173 block=0 → Attack 6×2  powers=[SLIPPERY_POWER:1,STRENGTH_POWER:2]

  → play card [0] target=0
  → play card [0] target=0
  → play card [1] target=0
  → end_turn → round transition
#### Round 7  (e=3/3 block=0 draw=5 disc=0)

  - hand:
    - [0] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [1] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [2] `DefendIronclad` cost=1 canPlay=True target=Self
    - [3] `Bash` cost=2 canPlay=True target=AnyEnemy
    - [4] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `VANTOM` 152/173 block=0 → Attack 27×1 + Unknown  powers=[STRENGTH_POWER:2]

  → play card [0] target=0
  → play card [0] target=0
  → play card [0]
  → end_turn → round transition
#### Round 8  (e=3/3 block=0 draw=0 disc=5)

  - hand:
    - [0] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [1] `DefendIronclad` cost=1 canPlay=True target=Self
    - [2] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [3] `DefendIronclad` cost=1 canPlay=True target=Self
    - [4] `DefendIronclad` cost=1 canPlay=True target=Self
  - enemies:
    - [0] `VANTOM` 140/173 block=0 → Buff  powers=[STRENGTH_POWER:2]

  → play card [0] target=0
  → play card [0]
  → play card [0] target=0
  → end_turn → round transition
#### Round 9  (e=3/3 block=0 draw=5 disc=0)

  - hand:
    - [0] `Bash` cost=2 canPlay=True target=AnyEnemy
    - [1] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [2] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [3] `DefendIronclad` cost=1 canPlay=True target=Self
    - [4] `DefendIronclad` cost=1 canPlay=True target=Self
  - enemies:
    - [0] `VANTOM` 128/173 block=0 → Attack 7×1  powers=[STRENGTH_POWER:4]

  → play card [0] target=0
  → play card [0] target=0
  → end_turn → round transition
#### Round 10  (e=3/3 block=0 draw=0 disc=5)

  - hand:
    - [0] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [1] `DefendIronclad` cost=1 canPlay=True target=Self
    - [2] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [3] `DefendIronclad` cost=1 canPlay=True target=Self
    - [4] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `VANTOM` 111/173 block=0 → Attack 6×2  powers=[STRENGTH_POWER:4,VULNERABLE_POWER:1]

  → play card [0] target=0
  → play card [0]
  → play card [0] target=0
  → end_turn → round transition
#### Round 11  (e=3/3 block=0 draw=5 disc=0)

  - hand:
    - [0] `DefendIronclad` cost=1 canPlay=True target=Self
    - [1] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [2] `Bash` cost=2 canPlay=True target=AnyEnemy
    - [3] `DefendIronclad` cost=1 canPlay=True target=Self
    - [4] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `VANTOM` 93/173 block=0 → Attack 27×1 + Unknown  powers=[STRENGTH_POWER:4]

  → play card [0]
  → play card [0] target=0
  → play card [1]
  → end_turn → round transition
#### Round 12  (e=3/3 block=0 draw=0 disc=5)

  - hand:
    - [0] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [1] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [2] `DefendIronclad` cost=1 canPlay=True target=Self
    - [3] `DefendIronclad` cost=1 canPlay=True target=Self
    - [4] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `VANTOM` 87/173 block=0 → Buff  powers=[STRENGTH_POWER:4]

  → play card [0] target=0
  → play card [0] target=0
  → play card [0]
  → end_turn → round transition
#### Round 13  (e=3/3 block=0 draw=5 disc=0)

  - hand:
    - [0] `DefendIronclad` cost=1 canPlay=True target=Self
    - [1] `DefendIronclad` cost=1 canPlay=True target=Self
    - [2] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [3] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [4] `Bash` cost=2 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `VANTOM` 75/173 block=0 → Attack 7×1  powers=[STRENGTH_POWER:6]

  → play card [0]
  → play card [0]
  → play card [0] target=0
  → end_turn → round transition
#### Round 14  (e=3/3 block=0 draw=0 disc=5)

  - hand:
    - [0] `DefendIronclad` cost=1 canPlay=True target=Self
    - [1] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [2] `DefendIronclad` cost=1 canPlay=True target=Self
    - [3] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [4] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `VANTOM` 69/173 block=0 → Attack 6×2  powers=[STRENGTH_POWER:6]

  → play card [0]
  → play card [0] target=0
  → play card [0]
  → end_turn → round transition
#### Round 15  (e=3/3 block=0 draw=5 disc=0)

  - hand:
    - [0] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [1] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [2] `Bash` cost=2 canPlay=True target=AnyEnemy
    - [3] `DefendIronclad` cost=1 canPlay=True target=Self
    - [4] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `VANTOM` 63/173 block=0 → Attack 27×1 + Unknown  powers=[STRENGTH_POWER:6]

  → play card [0] target=0
  → play card [0] target=0
  → play card [1]
  → end_turn → round transition
#### Round 16  (e=3/3 block=0 draw=0 disc=5)

  - hand:
    - [0] `DefendIronclad` cost=1 canPlay=True target=Self
    - [1] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [2] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [3] `DefendIronclad` cost=1 canPlay=True target=Self
    - [4] `DefendIronclad` cost=1 canPlay=True target=Self
  - enemies:
    - [0] `VANTOM` 51/173 block=0 → Buff  powers=[STRENGTH_POWER:6]

  → play card [0]
  → play card [0] target=0
  → play card [0] target=0
  → end_turn → round transition
#### Round 17  (e=3/3 block=0 draw=5 disc=0)

  - hand:
    - [0] `DefendIronclad` cost=1 canPlay=True target=Self
    - [1] `DefendIronclad` cost=1 canPlay=True target=Self
    - [2] `Bash` cost=2 canPlay=True target=AnyEnemy
    - [3] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [4] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `VANTOM` 39/173 block=0 → Attack 7×1  powers=[STRENGTH_POWER:8]

  → play card [0]
  → play card [0]
  → play card [1] target=0
  → end_turn → round transition
#### Round 18  (e=3/3 block=0 draw=0 disc=5)

  - hand:
    - [0] `DefendIronclad` cost=1 canPlay=True target=Self
    - [1] `DefendIronclad` cost=1 canPlay=True target=Self
    - [2] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [3] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [4] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `VANTOM` 33/173 block=0 → Attack 6×2  powers=[STRENGTH_POWER:8]

  → play card [0]
  → play card [0]
  → play card [0] target=0
  → end_turn → round transition
  - rewards offered:
    - [0] gold 100g  canSkip=False
    - [1] card cards=[`CrimsonMantle`(cost=1), `Conflagration`(cost=1), `Stoke`(cost=1)]  canSkip=True

  - rewards offered:
    - [0] gold 100g  canSkip=False
    - [1] card cards=[`CrimsonMantle`(cost=1), `Conflagration`(cost=1), `Stoke`(cost=1)]  canSkip=True

  - heal → hp=80/80

  - rewards offered:
    - [0] gold 100g  canSkip=False
    - [1] card cards=[`CrimsonMantle`(cost=1), `Conflagration`(cost=1), `Stoke`(cost=1)]  canSkip=True

  → claim reward [0] → hp=80 room=BossRoom
  - rewards offered:
    - [0] card cards=[`CrimsonMantle`(cost=1), `Conflagration`(cost=1), `Stoke`(cost=1)]  canSkip=True

  - rewards offered:
    - [0] card cards=[`CrimsonMantle`(cost=1), `Conflagration`(cost=1), `Stoke`(cost=1)]  canSkip=True

## Floor 17: MapRoom  (hp=80)


  → skip reward [0] → hp=80 room=MapRoom
  - combat ended (hp=80)

## Floor 0: MapRoom  (hp=80)

  - map options: (3,0):Unknown, (1,1):Monster, (3,1):Monster

  → enter_next_act → MapRoom floor=0
## Floor 2: CombatRoom  (hp=80)


  → pick map (1,1) → CombatRoom floor=2
### Combat #1 on floor 2

#### Round 1  (e=3/3 block=0 draw=5 disc=0)

  - hand:
    - [0] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [1] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [2] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [3] `DefendIronclad` cost=1 canPlay=True target=Self
    - [4] `DefendIronclad` cost=1 canPlay=True target=Self
  - enemies:
    - [0] `BOWLBUG_ROCK` 46/46 block=0 → Attack 15×1  powers=[IMBALANCED_POWER:1]
    - [1] `BOWLBUG_EGG` 22/22 block=0 → Attack 7×1 + Defend

  → play card [0] target=0
  → play card [0] target=0
  → play card [0] target=0
  → end_turn → round transition
#### Round 2  (e=3/3 block=0 draw=0 disc=5)

  - hand:
    - [0] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [1] `DefendIronclad` cost=1 canPlay=True target=Self
    - [2] `DefendIronclad` cost=1 canPlay=True target=Self
    - [3] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [4] `Bash` cost=2 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `BOWLBUG_ROCK` 28/46 block=0 → Attack 15×1  powers=[IMBALANCED_POWER:1]
    - [1] `BOWLBUG_EGG` 22/22 block=7 → Attack 7×1 + Defend

  → play card [0] target=0
  → play card [0]
  → play card [0]
  → end_turn → round transition
#### Round 3  (e=3/3 block=0 draw=5 disc=0)

  - hand:
    - [0] `DefendIronclad` cost=1 canPlay=True target=Self
    - [1] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [2] `Bash` cost=2 canPlay=True target=AnyEnemy
    - [3] `DefendIronclad` cost=1 canPlay=True target=Self
    - [4] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `BOWLBUG_ROCK` 22/46 block=0 → Attack 15×1  powers=[IMBALANCED_POWER:1]
    - [1] `BOWLBUG_EGG` 22/22 block=7 → Attack 7×1 + Defend

  → play card [0]
  → play card [0] target=0
  → play card [1]
  → end_turn → round transition
#### Round 4  (e=3/3 block=0 draw=0 disc=5)

  - hand:
    - [0] `DefendIronclad` cost=1 canPlay=True target=Self
    - [1] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [2] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [3] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [4] `DefendIronclad` cost=1 canPlay=True target=Self
  - enemies:
    - [0] `BOWLBUG_ROCK` 16/46 block=0 → Attack 15×1  powers=[IMBALANCED_POWER:1]
    - [1] `BOWLBUG_EGG` 22/22 block=7 → Attack 7×1 + Defend

  → play card [0]
  → play card [0] target=0
  → play card [0] target=0
  → end_turn → round transition
#### Round 5  (e=3/3 block=0 draw=5 disc=0)

  - hand:
    - [0] `DefendIronclad` cost=1 canPlay=True target=Self
    - [1] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [2] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [3] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [4] `Bash` cost=2 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `BOWLBUG_ROCK` 4/46 block=0 → Attack 15×1  powers=[IMBALANCED_POWER:1]
    - [1] `BOWLBUG_EGG` 22/22 block=7 → Attack 7×1 + Defend

  → play card [0]
  → play card [0] target=0
  → play card [0] target=0
  → end_turn → round transition
#### Round 6  (e=3/3 block=0 draw=0 disc=5)

  - hand:
    - [0] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [1] `DefendIronclad` cost=1 canPlay=True target=Self
    - [2] `DefendIronclad` cost=1 canPlay=True target=Self
    - [3] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [4] `DefendIronclad` cost=1 canPlay=True target=Self
  - enemies:
    - [0] `BOWLBUG_EGG` 22/22 block=7 → Attack 7×1 + Defend

  → play card [0] target=0
  → play card [0]
  → play card [0]
  → end_turn → round transition
#### Round 7  (e=3/3 block=0 draw=5 disc=0)

  - hand:
    - [0] `Bash` cost=2 canPlay=True target=AnyEnemy
    - [1] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [2] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [3] `DefendIronclad` cost=1 canPlay=True target=Self
    - [4] `DefendIronclad` cost=1 canPlay=True target=Self
  - enemies:
    - [0] `BOWLBUG_EGG` 22/22 block=7 → Attack 7×1 + Defend

  → play card [0] target=0
  → play card [0] target=0
  → end_turn → round transition
#### Round 8  (e=3/3 block=0 draw=0 disc=5)

  - hand:
    - [0] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [1] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [2] `DefendIronclad` cost=1 canPlay=True target=Self
    - [3] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [4] `DefendIronclad` cost=1 canPlay=True target=Self
  - enemies:
    - [0] `BOWLBUG_EGG` 12/22 block=7 → Attack 7×1 + Defend  powers=[VULNERABLE_POWER:1]

  → play card [0] target=0
  → play card [0] target=0
  → play card [0]
  → end_turn → round transition
#### Round 9  (e=3/3 block=0 draw=5 disc=0)

  - hand:
    - [0] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [1] `DefendIronclad` cost=1 canPlay=True target=Self
    - [2] `DefendIronclad` cost=1 canPlay=True target=Self
    - [3] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [4] `DefendIronclad` cost=1 canPlay=True target=Self
  - enemies:
    - [0] `BOWLBUG_EGG` 1/22 block=7 → Attack 7×1 + Defend

  → play card [0] target=0
  → play card [0]
  → play card [0]
  → end_turn → round transition
#### Round 10  (e=3/3 block=0 draw=0 disc=5)

  - hand:
    - [0] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [1] `Bash` cost=2 canPlay=True target=AnyEnemy
    - [2] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [3] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [4] `DefendIronclad` cost=1 canPlay=True target=Self
  - enemies:
    - [0] `BOWLBUG_EGG` 1/22 block=7 → Attack 7×1 + Defend

  → play card [0] target=0
  → play card [0] target=0
  - rewards offered:
    - [0] gold 12g  canSkip=False
    - [1] card cards=[`Hemokinesis`(cost=1), `Colossus`(cost=1), `FeelNoPain`(cost=1)]  canSkip=True

  - rewards offered:
    - [0] gold 12g  canSkip=False
    - [1] card cards=[`Hemokinesis`(cost=1), `Colossus`(cost=1), `FeelNoPain`(cost=1)]  canSkip=True

  → claim reward [0] → hp=60 room=CombatRoom
  - rewards offered:
    - [0] card cards=[`Hemokinesis`(cost=1), `Colossus`(cost=1), `FeelNoPain`(cost=1)]  canSkip=True

  - rewards offered:
    - [0] card cards=[`Hemokinesis`(cost=1), `Colossus`(cost=1), `FeelNoPain`(cost=1)]  canSkip=True

## Floor 2: MapRoom  (hp=60)

  - map options: (0,2):Monster

  → skip reward [0] → hp=60 room=MapRoom
  - combat ended (hp=60)

  - heal → hp=80/80

## Floor 3: CombatRoom  (hp=80)


  → pick map (0,2) → CombatRoom floor=3
### Combat #1 on floor 3

#### Round 1  (e=3/3 block=0 draw=5 disc=0)

  - hand:
    - [0] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [1] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [2] `DefendIronclad` cost=1 canPlay=True target=Self
    - [3] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [4] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `THIEVING_HOPPER` 79/79 block=0 → Attack 17×1 + Unknown  powers=[ESCAPE_ARTIST_POWER:5]

  → play card [0] target=0
  → play card [0] target=0
  → play card [0]
  → end_turn → round transition
#### Round 2  (e=3/3 block=0 draw=0 disc=5)

  - hand:
    - [0] `Bash` cost=2 canPlay=True target=AnyEnemy
    - [1] `DefendIronclad` cost=1 canPlay=True target=Self
    - [2] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [3] `DefendIronclad` cost=1 canPlay=True target=Self
    - [4] `DefendIronclad` cost=1 canPlay=True target=Self
  - enemies:
    - [0] `THIEVING_HOPPER` 67/79 block=0 → Buff  powers=[ESCAPE_ARTIST_POWER:5]

  → play card [0] target=0
  → play card [0]
  → end_turn → round transition
#### Round 3  (e=3/3 block=0 draw=5 disc=0)

  - hand:
    - [0] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [1] `DefendIronclad` cost=1 canPlay=True target=Self
    - [2] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [3] `Bash` cost=2 canPlay=True target=AnyEnemy
    - [4] `DefendIronclad` cost=1 canPlay=True target=Self
  - enemies:
    - [0] `THIEVING_HOPPER` 59/79 block=0 → Attack 21×1  powers=[ESCAPE_ARTIST_POWER:5,VULNERABLE_POWER:1]

  → play card [0] target=0
  → play card [0]
  → play card [0] target=0
  → end_turn → round transition
#### Round 4  (e=3/3 block=0 draw=0 disc=5)

  - hand:
    - [0] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [1] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [2] `DefendIronclad` cost=1 canPlay=True target=Self
    - [3] `DefendIronclad` cost=1 canPlay=True target=Self
    - [4] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `THIEVING_HOPPER` 41/79 block=0 → Attack 14×1  powers=[ESCAPE_ARTIST_POWER:5]

  → play card [0] target=0
  → play card [0] target=0
  → play card [0]
  → end_turn → round transition
#### Round 5  (e=3/3 block=0 draw=5 disc=0)

  - hand:
    - [0] `DefendIronclad` cost=1 canPlay=True target=Self
    - [1] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [2] `DefendIronclad` cost=1 canPlay=True target=Self
    - [3] `DefendIronclad` cost=1 canPlay=True target=Self
    - [4] `Bash` cost=2 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `THIEVING_HOPPER` 29/79 block=0 → Escape  powers=[ESCAPE_ARTIST_POWER:5]

  → play card [0]
  → play card [0] target=0
  → play card [0]
  → end_turn → round transition
#### Round 6  (e=3/3 block=0 draw=0 disc=5)

  - hand:
    - [0] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [1] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [2] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [3] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [4] `DefendIronclad` cost=1 canPlay=True target=Self
  - enemies:
    - [0] `THIEVING_HOPPER` 23/79 block=0 → Escape  powers=[ESCAPE_ARTIST_POWER:5]

  → play card [0] target=0
  → play card [0] target=0
  → play card [0] target=0
  → end_turn → round transition
#### Round 7  (e=3/3 block=0 draw=5 disc=0)

  - hand:
    - [0] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [1] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [2] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [3] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [4] `DefendIronclad` cost=1 canPlay=True target=Self
  - enemies:
    - [0] `THIEVING_HOPPER` 5/79 block=0 → Escape  powers=[ESCAPE_ARTIST_POWER:5]

  → play card [0] target=0
  - rewards offered:
    - [0] gold 13g  canSkip=False
    - [1] card cards=[`Bloodletting`(cost=0), `Breakthrough`(cost=1), `TwinStrike`(cost=1)]  canSkip=True

  - rewards offered:
    - [0] gold 13g  canSkip=False
    - [1] card cards=[`Bloodletting`(cost=0), `Breakthrough`(cost=1), `TwinStrike`(cost=1)]  canSkip=True

  → claim reward [0] → hp=80 room=CombatRoom
  - rewards offered:
    - [0] card cards=[`Bloodletting`(cost=0), `Breakthrough`(cost=1), `TwinStrike`(cost=1)]  canSkip=True

  - rewards offered:
    - [0] card cards=[`Bloodletting`(cost=0), `Breakthrough`(cost=1), `TwinStrike`(cost=1)]  canSkip=True

## Floor 3: MapRoom  (hp=80)

  - map options: (0,3):Monster

  → skip reward [0] → hp=80 room=MapRoom
  - combat ended (hp=80)

## Floor 4: CombatRoom  (hp=80)


  → pick map (0,3) → CombatRoom floor=4
### Combat #1 on floor 4

#### Round 1  (e=3/3 block=0 draw=5 disc=0)

  - hand:
    - [0] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [1] `Bash` cost=2 canPlay=True target=AnyEnemy
    - [2] `DefendIronclad` cost=1 canPlay=True target=Self
    - [3] `DefendIronclad` cost=1 canPlay=True target=Self
    - [4] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `HUNTER_KILLER` 121/121 block=0 → Debuff

  → play card [0] target=0
  → play card [0] target=0
  → end_turn → round transition
#### Round 2  (e=3/3 block=0 draw=0 disc=5)

  - hand:
    - [0] `DefendIronclad` cost=1 canPlay=True target=Self
    - [1] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [2] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [3] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [4] `DefendIronclad` cost=1 canPlay=True target=Self
  - enemies:
    - [0] `HUNTER_KILLER` 107/121 block=0 → Attack 17×1  powers=[VULNERABLE_POWER:1]
  - player powers: TENDER_POWER:1

  → play card [0]
  → play card [0] target=0
  → play card [0] target=0
  → end_turn → round transition
#### Round 3  (e=3/3 block=0 draw=5 disc=0)

  - hand:
    - [0] `DefendIronclad` cost=1 canPlay=True target=Self
    - [1] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [2] `DefendIronclad` cost=1 canPlay=True target=Self
    - [3] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [4] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `HUNTER_KILLER` 89/121 block=0 → Attack 7×3
  - player powers: TENDER_POWER:1

  → play card [0]
  → play card [0] target=0
  → play card [0]
  → end_turn → round transition
#### Round 4  (e=3/3 block=0 draw=0 disc=5)

  - hand:
    - [0] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [1] `DefendIronclad` cost=1 canPlay=True target=Self
    - [2] `Bash` cost=2 canPlay=True target=AnyEnemy
    - [3] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [4] `DefendIronclad` cost=1 canPlay=True target=Self
  - enemies:
    - [0] `HUNTER_KILLER` 83/121 block=0 → Attack 7×3
  - player powers: TENDER_POWER:1

  → play card [0] target=0
  → play card [0]
  → play card [1] target=0
  → end_turn → round transition
#### Round 5  (e=3/3 block=0 draw=5 disc=0)

  - hand:
    - [0] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [1] `DefendIronclad` cost=1 canPlay=True target=Self
    - [2] `DefendIronclad` cost=1 canPlay=True target=Self
    - [3] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [4] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `HUNTER_KILLER` 71/121 block=0 → Attack 17×1
  - player powers: TENDER_POWER:1

  → play card [0] target=0
  → play card [0]
  → play card [0]
  → end_turn → round transition
#### Round 6  (e=3/3 block=0 draw=0 disc=5)

  - hand:
    - [0] `DefendIronclad` cost=1 canPlay=True target=Self
    - [1] `Bash` cost=2 canPlay=True target=AnyEnemy
    - [2] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [3] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [4] `DefendIronclad` cost=1 canPlay=True target=Self
  - enemies:
    - [0] `HUNTER_KILLER` 65/121 block=0 → Attack 7×3
  - player powers: TENDER_POWER:1

  → play card [0]
  → play card [0] target=0
  → end_turn → round transition
#### Round 7  (e=3/3 block=0 draw=5 disc=0)

  - hand:
    - [0] `DefendIronclad` cost=1 canPlay=True target=Self
    - [1] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [2] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [3] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [4] `DefendIronclad` cost=1 canPlay=True target=Self
  - enemies:
    - [0] `HUNTER_KILLER` 57/121 block=0 → Attack 17×1  powers=[VULNERABLE_POWER:1]
  - player powers: TENDER_POWER:1

  → play card [0]
  → play card [0] target=0
  → play card [0] target=0
  → end_turn → round transition
#### Round 8  (e=3/3 block=0 draw=0 disc=5)

  - hand:
    - [0] `DefendIronclad` cost=1 canPlay=True target=Self
    - [1] `Bash` cost=2 canPlay=True target=AnyEnemy
    - [2] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [3] `DefendIronclad` cost=1 canPlay=True target=Self
    - [4] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `HUNTER_KILLER` 39/121 block=0 → Attack 7×3
  - player powers: TENDER_POWER:1

  → play card [0]
  → play card [0] target=0
  → end_turn → round transition
  - rewards offered:
    - [0] gold 11g  canSkip=False
    - [1] potion potion=`POTION.LIQUID_BRONZE (8414436)`  canSkip=False
    - [2] card cards=[`SetupStrike`(cost=1), `TwinStrike`(cost=1), `StoneArmor`(cost=1)]  canSkip=True

  - rewards offered:
    - [0] gold 11g  canSkip=False
    - [1] potion potion=`POTION.LIQUID_BRONZE (8414436)`  canSkip=False
    - [2] card cards=[`SetupStrike`(cost=1), `TwinStrike`(cost=1), `StoneArmor`(cost=1)]  canSkip=True

  - heal → hp=80/80

  - rewards offered:
    - [0] gold 11g  canSkip=False
    - [1] potion potion=`POTION.LIQUID_BRONZE (8414436)`  canSkip=False
    - [2] card cards=[`SetupStrike`(cost=1), `TwinStrike`(cost=1), `StoneArmor`(cost=1)]  canSkip=True

  → claim reward [0] → hp=80 room=CombatRoom
  - rewards offered:
    - [0] potion potion=`POTION.LIQUID_BRONZE (8414436)`  canSkip=False
    - [1] card cards=[`SetupStrike`(cost=1), `TwinStrike`(cost=1), `StoneArmor`(cost=1)]  canSkip=True

  - rewards offered:
    - [0] potion potion=`POTION.LIQUID_BRONZE (8414436)`  canSkip=False
    - [1] card cards=[`SetupStrike`(cost=1), `TwinStrike`(cost=1), `StoneArmor`(cost=1)]  canSkip=True

  → claim reward [0] → hp=80 room=CombatRoom
  - rewards offered:
    - [0] card cards=[`SetupStrike`(cost=1), `TwinStrike`(cost=1), `StoneArmor`(cost=1)]  canSkip=True

  - rewards offered:
    - [0] card cards=[`SetupStrike`(cost=1), `TwinStrike`(cost=1), `StoneArmor`(cost=1)]  canSkip=True

## Floor 4: MapRoom  (hp=80)

  - map options: (0,4):Unknown

  → skip reward [0] → hp=80 room=MapRoom
  - combat ended (hp=80)

## Floor 5: MerchantRoom  (hp=80)


  → pick map (0,4) → MerchantRoom floor=5
## Floor 5: MapRoom  (hp=80)

  - map options: (0,5):Unknown

  → leave merchant → MapRoom
## Floor 6: EventRoom  (hp=80)

  - event options:
    - [0] `POTION_COURIER.pages.INITIAL.options.GRAB_POTIONS`
    - [1] `POTION_COURIER.pages.INITIAL.options.RANSACK`

  → pick map (0,5) → EventRoom floor=6
## Floor 6: MapRoom  (hp=80)

  - map options: (0,6):RestSite

  → pick event option [1] → MapRoom hp=80
## Floor 7: RestSiteRoom  (hp=80)


  → pick map (0,6) → RestSiteRoom floor=7
## Floor 7: MapRoom  (hp=80)

  - map options: (1,7):Unknown

  → pick rest option [0] → MapRoom hp=80
## Floor 8: CombatRoom  (hp=80)


  → pick map (1,7) → CombatRoom floor=8
### Combat #1 on floor 8

#### Round 1  (e=3/3 block=0 draw=5 disc=0)

  - hand:
    - [0] `DefendIronclad` cost=1 canPlay=True target=Self
    - [1] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [2] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [3] `Bash` cost=2 canPlay=True target=AnyEnemy
    - [4] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `SPINY_TOAD` 118/118 block=0 → Buff

  → play card [0]
  → play card [0] target=0
  → play card [0] target=0
  → end_turn → round transition
#### Round 2  (e=3/3 block=0 draw=0 disc=5)

  - hand:
    - [0] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [1] `DefendIronclad` cost=1 canPlay=True target=Self
    - [2] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [3] `DefendIronclad` cost=1 canPlay=True target=Self
    - [4] `DefendIronclad` cost=1 canPlay=True target=Self
  - enemies:
    - [0] `SPINY_TOAD` 106/118 block=0 → Attack 23×1  powers=[THORNS_POWER:5]

  → play card [0] target=0
  → play card [0]
  → play card [0] target=0
  → end_turn → round transition
#### Round 3  (e=3/3 block=0 draw=5 disc=0)

  - hand:
    - [0] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [1] `DefendIronclad` cost=1 canPlay=True target=Self
    - [2] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [3] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [4] `DefendIronclad` cost=1 canPlay=True target=Self
  - enemies:
    - [0] `SPINY_TOAD` 94/118 block=0 → Attack 17×1

  → play card [0] target=0
  → play card [0]
  → play card [0] target=0
  → end_turn → round transition
#### Round 4  (e=3/3 block=0 draw=0 disc=5)

  - hand:
    - [0] `Bash` cost=2 canPlay=True target=AnyEnemy
    - [1] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [2] `DefendIronclad` cost=1 canPlay=True target=Self
    - [3] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [4] `DefendIronclad` cost=1 canPlay=True target=Self
  - enemies:
    - [0] `SPINY_TOAD` 82/118 block=0 → Buff

  → play card [0] target=0
  → play card [0] target=0
  → end_turn → round transition
#### Round 5  (e=3/3 block=0 draw=5 disc=0)

  - hand:
    - [0] `Bash` cost=2 canPlay=True target=AnyEnemy
    - [1] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [2] `DefendIronclad` cost=1 canPlay=True target=Self
    - [3] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [4] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `SPINY_TOAD` 65/118 block=0 → Attack 23×1  powers=[VULNERABLE_POWER:1,THORNS_POWER:5]

  → play card [0] target=0
  → play card [0] target=0
  → end_turn → round transition
#### Round 6  (e=3/3 block=0 draw=0 disc=5)

  - hand:
    - [0] `DefendIronclad` cost=1 canPlay=True target=Self
    - [1] `DefendIronclad` cost=1 canPlay=True target=Self
    - [2] `DefendIronclad` cost=1 canPlay=True target=Self
    - [3] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [4] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `SPINY_TOAD` 44/118 block=0 → Attack 17×1  powers=[VULNERABLE_POWER:2]

  → play card [0]
  → play card [0]
  → play card [0]
  → end_turn → round transition
#### Round 7  (e=3/3 block=0 draw=5 disc=0)

  - hand:
    - [0] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [1] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [2] `DefendIronclad` cost=1 canPlay=True target=Self
    - [3] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [4] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `SPINY_TOAD` 44/118 block=0 → Buff  powers=[VULNERABLE_POWER:1]

  → play card [0] target=0
  → play card [0] target=0
  → play card [0]
  → end_turn → round transition
#### Round 8  (e=3/3 block=0 draw=0 disc=5)

  - hand:
    - [0] `DefendIronclad` cost=1 canPlay=True target=Self
    - [1] `DefendIronclad` cost=1 canPlay=True target=Self
    - [2] `Bash` cost=2 canPlay=True target=AnyEnemy
    - [3] `DefendIronclad` cost=1 canPlay=True target=Self
    - [4] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `SPINY_TOAD` 26/118 block=0 → Attack 23×1  powers=[THORNS_POWER:5]

  → play card [0]
  → play card [0]
  → play card [1]
  → end_turn → round transition
  - rewards offered:
    - [0] gold 14g  canSkip=False
    - [1] potion potion=`POTION.BLESSING_OF_THE_FORGE (61306625)`  canSkip=False
    - [2] card cards=[`Headbutt`(cost=1), `Conflagration`(cost=1), `MoltenFist`(cost=1)]  canSkip=True

  - rewards offered:
    - [0] gold 14g  canSkip=False
    - [1] potion potion=`POTION.BLESSING_OF_THE_FORGE (61306625)`  canSkip=False
    - [2] card cards=[`Headbutt`(cost=1), `Conflagration`(cost=1), `MoltenFist`(cost=1)]  canSkip=True

  - heal → hp=80/80

  - rewards offered:
    - [0] gold 14g  canSkip=False
    - [1] potion potion=`POTION.BLESSING_OF_THE_FORGE (61306625)`  canSkip=False
    - [2] card cards=[`Headbutt`(cost=1), `Conflagration`(cost=1), `MoltenFist`(cost=1)]  canSkip=True

  → claim reward [0] → hp=80 room=CombatRoom
  - rewards offered:
    - [0] potion potion=`POTION.BLESSING_OF_THE_FORGE (61306625)`  canSkip=False
    - [1] card cards=[`Headbutt`(cost=1), `Conflagration`(cost=1), `MoltenFist`(cost=1)]  canSkip=True

  - rewards offered:
    - [0] potion potion=`POTION.BLESSING_OF_THE_FORGE (61306625)`  canSkip=False
    - [1] card cards=[`Headbutt`(cost=1), `Conflagration`(cost=1), `MoltenFist`(cost=1)]  canSkip=True

  → claim reward [0] → hp=80 room=CombatRoom
  - rewards offered:
    - [0] card cards=[`Headbutt`(cost=1), `Conflagration`(cost=1), `MoltenFist`(cost=1)]  canSkip=True

  - rewards offered:
    - [0] card cards=[`Headbutt`(cost=1), `Conflagration`(cost=1), `MoltenFist`(cost=1)]  canSkip=True

## Floor 8: MapRoom  (hp=80)

  - map options: (2,8):Treasure

  → skip reward [0] → hp=80 room=MapRoom
  - combat ended (hp=80)

## Floor 9: TreasureRoom  (hp=80)


  → pick map (2,8) → TreasureRoom floor=9
## Floor 9: MapRoom  (hp=80)

  - map options: (2,9):Elite, (1,9):Monster, (3,9):Unknown

  → leave treasure → MapRoom relics=`BURNING_BLOOD`, `ODDLY_SMOOTH_STONE`, `GORGET`, `JUZU_BRACELET`
## Floor 10: CombatRoom  (hp=80)


  → pick map (1,9) → CombatRoom floor=10
### Combat #1 on floor 10

#### Round 1  (e=3/3 block=0 draw=5 disc=0)

  - hand:
    - [0] `DefendIronclad` cost=1 canPlay=True target=Self
    - [1] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [2] `Bash` cost=2 canPlay=True target=AnyEnemy
    - [3] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [4] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `EXOSKELETON` 24/24 block=0 → Attack 1×3  powers=[HARD_TO_KILL_POWER:9]
    - [1] `EXOSKELETON` 26/26 block=0 → Attack 8×1  powers=[HARD_TO_KILL_POWER:9]
    - [2] `EXOSKELETON` 27/27 block=0 → Buff  powers=[HARD_TO_KILL_POWER:9]
    - [3] `EXOSKELETON` 25/25 block=0 → Attack 1×3  powers=[HARD_TO_KILL_POWER:9]

  → play card [0]
  → play card [0] target=0
  → play card [1] target=0
  → end_turn → round transition
#### Round 2  (e=3/3 block=0 draw=0 disc=5)

  - hand:
    - [0] `DefendIronclad` cost=1 canPlay=True target=Self
    - [1] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [2] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [3] `DefendIronclad` cost=1 canPlay=True target=Self
    - [4] `DefendIronclad` cost=1 canPlay=True target=Self
  - enemies:
    - [0] `EXOSKELETON` 12/24 block=0 → Attack 8×1  powers=[HARD_TO_KILL_POWER:9]
    - [1] `EXOSKELETON` 26/26 block=0 → Buff  powers=[HARD_TO_KILL_POWER:9]
    - [2] `EXOSKELETON` 27/27 block=0 → Attack 1×3  powers=[HARD_TO_KILL_POWER:9,STRENGTH_POWER:2]
    - [3] `EXOSKELETON` 25/25 block=0 → Attack 8×1  powers=[HARD_TO_KILL_POWER:9]

  → play card [0]
  → play card [0] target=0
  → play card [0] target=0
  → end_turn → round transition
#### Round 3  (e=3/3 block=0 draw=5 disc=0)

  - hand:
    - [0] `Bash` cost=2 canPlay=True target=AnyEnemy
    - [1] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [2] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [3] `DefendIronclad` cost=1 canPlay=True target=Self
    - [4] `DefendIronclad` cost=1 canPlay=True target=Self
  - enemies:
    - [0] `EXOSKELETON` 26/26 block=0 → Attack 1×3  powers=[HARD_TO_KILL_POWER:9,STRENGTH_POWER:2]
    - [1] `EXOSKELETON` 27/27 block=0 → Attack 8×1  powers=[HARD_TO_KILL_POWER:9,STRENGTH_POWER:2]
    - [2] `EXOSKELETON` 25/25 block=0 → Buff  powers=[HARD_TO_KILL_POWER:9]

  → play card [0] target=0
  → play card [0] target=0
  → end_turn → round transition
#### Round 4  (e=3/3 block=0 draw=0 disc=5)

  - hand:
    - [0] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [1] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [2] `DefendIronclad` cost=1 canPlay=True target=Self
    - [3] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [4] `DefendIronclad` cost=1 canPlay=True target=Self
  - enemies:
    - [0] `EXOSKELETON` 9/26 block=0 → Attack 8×1  powers=[HARD_TO_KILL_POWER:9,STRENGTH_POWER:2,VULNERABLE_POWER:1]
    - [1] `EXOSKELETON` 27/27 block=0 → Buff  powers=[HARD_TO_KILL_POWER:9,STRENGTH_POWER:2]
    - [2] `EXOSKELETON` 25/25 block=0 → Attack 1×3  powers=[HARD_TO_KILL_POWER:9,STRENGTH_POWER:2]

  → play card [0] target=0
  → play card [0] target=0
  → play card [0]
  → end_turn → round transition
#### Round 5  (e=3/3 block=0 draw=5 disc=0)

  - hand:
    - [0] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [1] `DefendIronclad` cost=1 canPlay=True target=Self
    - [2] `DefendIronclad` cost=1 canPlay=True target=Self
    - [3] `DefendIronclad` cost=1 canPlay=True target=Self
    - [4] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `EXOSKELETON` 21/27 block=0 → Attack 1×3  powers=[HARD_TO_KILL_POWER:9,STRENGTH_POWER:4]
    - [1] `EXOSKELETON` 25/25 block=0 → Attack 8×1  powers=[HARD_TO_KILL_POWER:9,STRENGTH_POWER:2]

  → play card [0] target=0
  → play card [0]
  → play card [0]
  → end_turn → round transition
#### Round 6  (e=3/3 block=0 draw=0 disc=5)

  - hand:
    - [0] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [1] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [2] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [3] `DefendIronclad` cost=1 canPlay=True target=Self
    - [4] `Bash` cost=2 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `EXOSKELETON` 15/27 block=0 → Attack 8×1  powers=[HARD_TO_KILL_POWER:9,STRENGTH_POWER:4]
    - [1] `EXOSKELETON` 25/25 block=0 → Buff  powers=[HARD_TO_KILL_POWER:9,STRENGTH_POWER:2]

  → play card [0] target=0
  → play card [0] target=0
  → play card [0] target=0
  → end_turn → round transition
#### Round 7  (e=3/3 block=0 draw=5 disc=0)

  - hand:
    - [0] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [1] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [2] `DefendIronclad` cost=1 canPlay=True target=Self
    - [3] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [4] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `EXOSKELETON` 25/25 block=0 → Attack 1×3  powers=[HARD_TO_KILL_POWER:9,STRENGTH_POWER:4]

  → play card [0] target=0
  → play card [0] target=0
  → play card [0]
  → end_turn → round transition
#### Round 8  (e=3/3 block=0 draw=0 disc=5)

  - hand:
    - [0] `DefendIronclad` cost=1 canPlay=True target=Self
    - [1] `DefendIronclad` cost=1 canPlay=True target=Self
    - [2] `DefendIronclad` cost=1 canPlay=True target=Self
    - [3] `Bash` cost=2 canPlay=True target=AnyEnemy
    - [4] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `EXOSKELETON` 13/25 block=0 → Attack 8×1  powers=[HARD_TO_KILL_POWER:9,STRENGTH_POWER:4]

  → play card [0]
  → play card [0]
  → play card [0]
  → end_turn → round transition
#### Round 9  (e=3/3 block=0 draw=5 disc=0)

  - hand:
    - [0] `Bash` cost=2 canPlay=True target=AnyEnemy
    - [1] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [2] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [3] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [4] `DefendIronclad` cost=1 canPlay=True target=Self
  - enemies:
    - [0] `EXOSKELETON` 13/25 block=0 → Buff  powers=[HARD_TO_KILL_POWER:9,STRENGTH_POWER:4]

  → play card [0] target=0
  → play card [0] target=0
  - rewards offered:
    - [0] gold 13g  canSkip=False
    - [1] potion potion=`POTION.FIRE_POTION (56489102)`  canSkip=False
    - [2] card cards=[`BodySlam`(cost=1), `ForgottenRitual`(cost=1), `Breakthrough`(cost=1)]  canSkip=True

  - rewards offered:
    - [0] gold 13g  canSkip=False
    - [1] potion potion=`POTION.FIRE_POTION (56489102)`  canSkip=False
    - [2] card cards=[`BodySlam`(cost=1), `ForgottenRitual`(cost=1), `Breakthrough`(cost=1)]  canSkip=True

  → claim reward [0] → hp=11 room=CombatRoom
  - rewards offered:
    - [0] potion potion=`POTION.FIRE_POTION (56489102)`  canSkip=False
    - [1] card cards=[`BodySlam`(cost=1), `ForgottenRitual`(cost=1), `Breakthrough`(cost=1)]  canSkip=True

  - rewards offered:
    - [0] potion potion=`POTION.FIRE_POTION (56489102)`  canSkip=False
    - [1] card cards=[`BodySlam`(cost=1), `ForgottenRitual`(cost=1), `Breakthrough`(cost=1)]  canSkip=True

  → claim reward [0] → hp=11 room=CombatRoom
  - rewards offered:
    - [0] card cards=[`BodySlam`(cost=1), `ForgottenRitual`(cost=1), `Breakthrough`(cost=1)]  canSkip=True

  - rewards offered:
    - [0] card cards=[`BodySlam`(cost=1), `ForgottenRitual`(cost=1), `Breakthrough`(cost=1)]  canSkip=True

## Floor 10: MapRoom  (hp=11)

  - map options: (0,10):Unknown, (1,10):RestSite

  → skip reward [0] → hp=11 room=MapRoom
  - combat ended (hp=11)

  - heal → hp=80/80

## Floor 11: EventRoom  (hp=80)

  - event options:
    - [0] `TEA_MASTER.pages.INITIAL.options.BONE_TEA`
    - [1] `TEA_MASTER.pages.INITIAL.options.EMBER_TEA`
    - [2] `TEA_MASTER.pages.INITIAL.options.TEA_OF_DISCOURTESY`

  → pick map (0,10) → EventRoom floor=11
## Floor 11: MapRoom  (hp=80)

  - map options: (0,11):Monster

  → pick event option [2] → MapRoom hp=80
## Floor 12: CombatRoom  (hp=80)


  → pick map (0,11) → CombatRoom floor=12
### Combat #1 on floor 12

#### Round 1  (e=3/3 block=0 draw=5 disc=0)

  - hand:
    - [0] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [1] `DefendIronclad` cost=1 canPlay=True target=Self
    - [2] `DefendIronclad` cost=1 canPlay=True target=Self
    - [3] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [4] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `BOWLBUG_ROCK` 47/47 block=0 → Attack 15×1  powers=[IMBALANCED_POWER:1]
    - [1] `BOWLBUG_EGG` 22/22 block=0 → Attack 7×1 + Defend
    - [2] `BOWLBUG_NECTAR` 38/38 block=0 → Attack 3×1

  → play card [0] target=0
  → play card [0]
  → play card [0]
  → end_turn → round transition
#### Round 2  (e=3/3 block=0 draw=0 disc=5)

  - hand:
    - [0] `Bash` cost=2 canPlay=True target=AnyEnemy
    - [1] `DefendIronclad` cost=1 canPlay=True target=Self
    - [2] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [3] `DefendIronclad` cost=1 canPlay=True target=Self
    - [4] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `BOWLBUG_ROCK` 41/47 block=0 → Attack 15×1  powers=[IMBALANCED_POWER:1]
    - [1] `BOWLBUG_EGG` 22/22 block=7 → Attack 7×1 + Defend
    - [2] `BOWLBUG_NECTAR` 38/38 block=0 → Buff

  → play card [0] target=0
  → play card [0]
  → end_turn → round transition
#### Round 3  (e=3/3 block=0 draw=5 disc=0)

  - hand:
    - [0] `DefendIronclad` cost=1 canPlay=True target=Self
    - [1] `DefendIronclad` cost=1 canPlay=True target=Self
    - [2] `Bash` cost=2 canPlay=True target=AnyEnemy
    - [3] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [4] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `BOWLBUG_ROCK` 33/47 block=0 → Attack 15×1  powers=[IMBALANCED_POWER:1,VULNERABLE_POWER:1]
    - [1] `BOWLBUG_EGG` 22/22 block=7 → Attack 7×1 + Defend
    - [2] `BOWLBUG_NECTAR` 38/38 block=0 → Attack 3×1  powers=[STRENGTH_POWER:15]

  → play card [0]
  → play card [0]
  → play card [1] target=0
  → end_turn → round transition
#### Round 4  (e=3/3 block=0 draw=0 disc=5)

  - hand:
    - [0] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [1] `DefendIronclad` cost=1 canPlay=True target=Self
    - [2] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [3] `DefendIronclad` cost=1 canPlay=True target=Self
    - [4] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `BOWLBUG_ROCK` 24/47 block=0 → Attack 15×1  powers=[IMBALANCED_POWER:1]
    - [1] `BOWLBUG_EGG` 22/22 block=7 → Attack 7×1 + Defend
    - [2] `BOWLBUG_NECTAR` 38/38 block=0 → Attack 3×1  powers=[STRENGTH_POWER:15]

  → play card [0] target=0
  → play card [0]
  → play card [0] target=0
  → end_turn → round transition
#### Round 5  (e=3/3 block=0 draw=5 disc=0)

  - hand:
    - [0] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [1] `DefendIronclad` cost=1 canPlay=True target=Self
    - [2] `DefendIronclad` cost=1 canPlay=True target=Self
    - [3] `DefendIronclad` cost=1 canPlay=True target=Self
    - [4] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `BOWLBUG_ROCK` 12/47 block=0 → Attack 15×1  powers=[IMBALANCED_POWER:1]
    - [1] `BOWLBUG_EGG` 22/22 block=7 → Attack 7×1 + Defend
    - [2] `BOWLBUG_NECTAR` 38/38 block=0 → Attack 3×1  powers=[STRENGTH_POWER:15]

  → play card [0] target=0
  → play card [0]
  → play card [0]
  → end_turn → round transition
#### Round 6  (e=3/3 block=0 draw=0 disc=5)

  - hand:
    - [0] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [1] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [2] `Bash` cost=2 canPlay=True target=AnyEnemy
    - [3] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [4] `DefendIronclad` cost=1 canPlay=True target=Self
  - enemies:
    - [0] `BOWLBUG_ROCK` 6/47 block=0 → Attack 15×1  powers=[IMBALANCED_POWER:1]
    - [1] `BOWLBUG_EGG` 22/22 block=7 → Attack 7×1 + Defend
    - [2] `BOWLBUG_NECTAR` 38/38 block=0 → Attack 3×1  powers=[STRENGTH_POWER:15]

  → play card [0] target=0
  → play card [0] target=0
  → play card [1] target=0
  → end_turn → round transition
#### Round 7  (e=3/3 block=0 draw=5 disc=0)

  - hand:
    - [0] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [1] `Bash` cost=2 canPlay=True target=AnyEnemy
    - [2] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [3] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [4] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `BOWLBUG_EGG` 17/22 block=7 → Attack 7×1 + Defend
    - [1] `BOWLBUG_NECTAR` 38/38 block=0 → Attack 3×1  powers=[STRENGTH_POWER:15]

  → play card [0] target=0
  → play card [0] target=0
  → end_turn → round transition
  - rewards offered:
    - [0] gold 16g  canSkip=False
    - [1] card cards=[`Bloodletting`(cost=0), `SetupStrike`(cost=1), `Thunderclap`(cost=1)]  canSkip=True

  - rewards offered:
    - [0] gold 16g  canSkip=False
    - [1] card cards=[`Bloodletting`(cost=0), `SetupStrike`(cost=1), `Thunderclap`(cost=1)]  canSkip=True

  - heal → hp=80/80

  - rewards offered:
    - [0] gold 16g  canSkip=False
    - [1] card cards=[`Bloodletting`(cost=0), `SetupStrike`(cost=1), `Thunderclap`(cost=1)]  canSkip=True

  → claim reward [0] → hp=80 room=CombatRoom
  - rewards offered:
    - [0] card cards=[`Bloodletting`(cost=0), `SetupStrike`(cost=1), `Thunderclap`(cost=1)]  canSkip=True

  - rewards offered:
    - [0] card cards=[`Bloodletting`(cost=0), `SetupStrike`(cost=1), `Thunderclap`(cost=1)]  canSkip=True

## Floor 12: MapRoom  (hp=80)

  - map options: (0,12):Monster

  → skip reward [0] → hp=80 room=MapRoom
  - combat ended (hp=80)

## Floor 13: CombatRoom  (hp=80)


  → pick map (0,12) → CombatRoom floor=13
### Combat #1 on floor 13

#### Round 1  (e=3/3 block=0 draw=5 disc=0)

  - hand:
    - [0] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [1] `DefendIronclad` cost=1 canPlay=True target=Self
    - [2] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [3] `Bash` cost=2 canPlay=True target=AnyEnemy
    - [4] `DefendIronclad` cost=1 canPlay=True target=Self
  - enemies:
    - [0] `LOUSE_PROGENITOR` 136/136 block=0 → Attack 9×1 + Debuff  powers=[CURL_UP_POWER:14]

  → play card [0] target=0
  → play card [0]
  → play card [0] target=0
  → end_turn → round transition
#### Round 2  (e=3/3 block=0 draw=0 disc=5)

  - hand:
    - [0] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [1] `DefendIronclad` cost=1 canPlay=True target=Self
    - [2] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [3] `DefendIronclad` cost=1 canPlay=True target=Self
    - [4] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `LOUSE_PROGENITOR` 130/136 block=0 → Defend + Buff
  - player powers: FRAIL_POWER:2

  → play card [0] target=0
  → play card [0]
  → play card [0] target=0
  → end_turn → round transition
#### Round 3  (e=3/3 block=0 draw=5 disc=0)

  - hand:
    - [0] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [1] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [2] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [3] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [4] `DefendIronclad` cost=1 canPlay=True target=Self
  - enemies:
    - [0] `LOUSE_PROGENITOR` 118/136 block=14 → Attack 14×1  powers=[STRENGTH_POWER:5]
  - player powers: FRAIL_POWER:2

  → play card [0] target=0
  → play card [0] target=0
  → play card [0] target=0
  → end_turn → round transition
#### Round 4  (e=3/3 block=0 draw=0 disc=5)

  - hand:
    - [0] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [1] `Bash` cost=2 canPlay=True target=AnyEnemy
    - [2] `DefendIronclad` cost=1 canPlay=True target=Self
    - [3] `DefendIronclad` cost=1 canPlay=True target=Self
    - [4] `DefendIronclad` cost=1 canPlay=True target=Self
  - enemies:
    - [0] `LOUSE_PROGENITOR` 114/136 block=0 → Attack 9×1 + Debuff  powers=[STRENGTH_POWER:5]
  - player powers: FRAIL_POWER:2

  → play card [0] target=0
  → play card [0] target=0
  → end_turn → round transition
#### Round 5  (e=3/3 block=0 draw=5 disc=0)

  - hand:
    - [0] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [1] `Bash` cost=2 canPlay=True target=AnyEnemy
    - [2] `DefendIronclad` cost=1 canPlay=True target=Self
    - [3] `DefendIronclad` cost=1 canPlay=True target=Self
    - [4] `DefendIronclad` cost=1 canPlay=True target=Self
  - enemies:
    - [0] `LOUSE_PROGENITOR` 100/136 block=0 → Defend + Buff  powers=[STRENGTH_POWER:5,VULNERABLE_POWER:1]
  - player powers: FRAIL_POWER:4

  → play card [0] target=0
  → play card [0] target=0
  → end_turn → round transition
#### Round 6  (e=3/3 block=0 draw=0 disc=5)

  - hand:
    - [0] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [1] `DefendIronclad` cost=1 canPlay=True target=Self
    - [2] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [3] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [4] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `LOUSE_PROGENITOR` 79/136 block=14 → Attack 14×1  powers=[STRENGTH_POWER:10,VULNERABLE_POWER:2]
  - player powers: FRAIL_POWER:4

  → play card [0] target=0
  → play card [0]
  → play card [0] target=0
  → end_turn → round transition
#### Round 7  (e=3/3 block=0 draw=5 disc=0)

  - hand:
    - [0] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [1] `DefendIronclad` cost=1 canPlay=True target=Self
    - [2] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [3] `Bash` cost=2 canPlay=True target=AnyEnemy
    - [4] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `LOUSE_PROGENITOR` 75/136 block=0 → Attack 9×1 + Debuff  powers=[STRENGTH_POWER:10,VULNERABLE_POWER:1]
  - player powers: FRAIL_POWER:4

  → play card [0] target=0
  → play card [0]
  → play card [0] target=0
  → end_turn → round transition
#### Round 8  (e=3/3 block=0 draw=0 disc=5)

  - hand:
    - [0] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [1] `DefendIronclad` cost=1 canPlay=True target=Self
    - [2] `DefendIronclad` cost=1 canPlay=True target=Self
    - [3] `DefendIronclad` cost=1 canPlay=True target=Self
    - [4] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `LOUSE_PROGENITOR` 57/136 block=0 → Defend + Buff  powers=[STRENGTH_POWER:10]
  - player powers: FRAIL_POWER:6

  → play card [0] target=0
  → play card [0]
  → play card [0]
  → end_turn → round transition
#### Round 9  (e=3/3 block=0 draw=5 disc=0)

  - hand:
    - [0] `DefendIronclad` cost=1 canPlay=True target=Self
    - [1] `Bash` cost=2 canPlay=True target=AnyEnemy
    - [2] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [3] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [4] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `LOUSE_PROGENITOR` 51/136 block=14 → Attack 14×1  powers=[STRENGTH_POWER:15]
  - player powers: FRAIL_POWER:6

  → play card [0]
  → play card [0] target=0
  → end_turn → round transition
  - rewards offered:
    - [0] gold 11g  canSkip=False
    - [1] card cards=[`Juggling`(cost=1), `Hemokinesis`(cost=1), `Mangle`(cost=3)]  canSkip=True

  - rewards offered:
    - [0] gold 11g  canSkip=False
    - [1] card cards=[`Juggling`(cost=1), `Hemokinesis`(cost=1), `Mangle`(cost=3)]  canSkip=True

  - heal → hp=80/80

  - rewards offered:
    - [0] gold 11g  canSkip=False
    - [1] card cards=[`Juggling`(cost=1), `Hemokinesis`(cost=1), `Mangle`(cost=3)]  canSkip=True

  → claim reward [0] → hp=80 room=CombatRoom
  - rewards offered:
    - [0] card cards=[`Juggling`(cost=1), `Hemokinesis`(cost=1), `Mangle`(cost=3)]  canSkip=True

  - rewards offered:
    - [0] card cards=[`Juggling`(cost=1), `Hemokinesis`(cost=1), `Mangle`(cost=3)]  canSkip=True

## Floor 13: MapRoom  (hp=80)

  - map options: (0,13):Elite, (1,13):Monster

  → skip reward [0] → hp=80 room=MapRoom
  - combat ended (hp=80)

## Floor 14: CombatRoom  (hp=80)


  → pick map (1,13) → CombatRoom floor=14
### Combat #1 on floor 14

#### Round 1  (e=3/3 block=0 draw=5 disc=0)

  - hand:
    - [0] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [1] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [2] `DefendIronclad` cost=1 canPlay=True target=Self
    - [3] `DefendIronclad` cost=1 canPlay=True target=Self
    - [4] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `OVICOPTER` 125/125 block=0 → Unknown

  → play card [0] target=0
  → play card [0] target=0
  → play card [0]
  → end_turn → round transition
#### Round 2  (e=3/3 block=0 draw=0 disc=5)

  - hand:
    - [0] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [1] `DefendIronclad` cost=1 canPlay=True target=Self
    - [2] `Bash` cost=2 canPlay=True target=AnyEnemy
    - [3] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [4] `DefendIronclad` cost=1 canPlay=True target=Self
  - enemies:
    - [0] `TOUGH_EGG` 14/14 block=0 → Unknown  powers=[HATCH_POWER:1,MINION_POWER:1]
    - [1] `TOUGH_EGG` 17/17 block=0 → Unknown  powers=[HATCH_POWER:1,MINION_POWER:1]
    - [2] `TOUGH_EGG` 16/16 block=0 → Unknown  powers=[HATCH_POWER:1,MINION_POWER:1]
    - [3] `OVICOPTER` 113/125 block=0 → Attack 16×1

  → play card [0] target=0
  → play card [0]
  → play card [1] target=0
  → end_turn → round transition
#### Round 3  (e=3/3 block=0 draw=5 disc=0)

  - hand:
    - [0] `Bash` cost=2 canPlay=True target=AnyEnemy
    - [1] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [2] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [3] `DefendIronclad` cost=1 canPlay=True target=Self
    - [4] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `TOUGH_EGG` 21/21 block=0 → Attack 4×1  powers=[MINION_POWER:1]
    - [1] `TOUGH_EGG` 19/19 block=0 → Attack 4×1  powers=[MINION_POWER:1]
    - [2] `TOUGH_EGG` 21/21 block=0 → Attack 4×1  powers=[MINION_POWER:1]
    - [3] `OVICOPTER` 113/125 block=0 → Attack 7×1 + Debuff

  → play card [0] target=0
  → play card [0] target=0
  → end_turn → round transition
#### Round 4  (e=3/3 block=0 draw=0 disc=5)

  - hand:
    - [0] `DefendIronclad` cost=1 canPlay=True target=Self
    - [1] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [2] `DefendIronclad` cost=1 canPlay=True target=Self
    - [3] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [4] `DefendIronclad` cost=1 canPlay=True target=Self
  - enemies:
    - [0] `TOUGH_EGG` 4/21 block=0 → Attack 4×1  powers=[MINION_POWER:1,VULNERABLE_POWER:1]
    - [1] `TOUGH_EGG` 19/19 block=0 → Attack 4×1  powers=[MINION_POWER:1]
    - [2] `TOUGH_EGG` 21/21 block=0 → Attack 4×1  powers=[MINION_POWER:1]
    - [3] `OVICOPTER` 113/125 block=0 → Buff
  - player powers: VULNERABLE_POWER:2

  → play card [0]
  → play card [0] target=0
  → play card [0]
  → end_turn → round transition
#### Round 5  (e=3/3 block=0 draw=5 disc=0)

  - hand:
    - [0] `DefendIronclad` cost=1 canPlay=True target=Self
    - [1] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [2] `DefendIronclad` cost=1 canPlay=True target=Self
    - [3] `DefendIronclad` cost=1 canPlay=True target=Self
    - [4] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `TOUGH_EGG` 19/19 block=0 → Attack 4×1  powers=[MINION_POWER:1]
    - [1] `TOUGH_EGG` 21/21 block=0 → Attack 4×1  powers=[MINION_POWER:1]
    - [2] `OVICOPTER` 113/125 block=0 → Attack 16×1  powers=[STRENGTH_POWER:3]
  - player powers: VULNERABLE_POWER:2

  → play card [0]
  → play card [0] target=0
  → play card [0]
  → end_turn → round transition
#### Round 6  (e=3/3 block=0 draw=0 disc=5)

  - hand:
    - [0] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [1] `DefendIronclad` cost=1 canPlay=True target=Self
    - [2] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [3] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [4] `Bash` cost=2 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `TOUGH_EGG` 13/19 block=0 → Attack 4×1  powers=[MINION_POWER:1]
    - [1] `TOUGH_EGG` 21/21 block=0 → Attack 4×1  powers=[MINION_POWER:1]
    - [2] `OVICOPTER` 113/125 block=0 → Attack 7×1 + Debuff  powers=[STRENGTH_POWER:3]
  - player powers: VULNERABLE_POWER:2

  → play card [0] target=0
  → play card [0]
  → play card [0] target=0
  → end_turn → round transition
#### Round 7  (e=3/3 block=0 draw=5 disc=0)

  - hand:
    - [0] `DefendIronclad` cost=1 canPlay=True target=Self
    - [1] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [2] `DefendIronclad` cost=1 canPlay=True target=Self
    - [3] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [4] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `TOUGH_EGG` 1/19 block=0 → Attack 4×1  powers=[MINION_POWER:1]
    - [1] `TOUGH_EGG` 21/21 block=0 → Attack 4×1  powers=[MINION_POWER:1]
    - [2] `OVICOPTER` 113/125 block=0 → Unknown  powers=[STRENGTH_POWER:3]
  - player powers: VULNERABLE_POWER:4

  → play card [0]
  → play card [0] target=0
  → play card [0]
  → end_turn → round transition
#### Round 8  (e=3/3 block=0 draw=0 disc=5)

  - hand:
    - [0] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [1] `Bash` cost=2 canPlay=True target=AnyEnemy
    - [2] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [3] `DefendIronclad` cost=1 canPlay=True target=Self
    - [4] `DefendIronclad` cost=1 canPlay=True target=Self
  - enemies:
    - [0] `TOUGH_EGG` 14/14 block=0 → Unknown  powers=[HATCH_POWER:1,MINION_POWER:1]
    - [1] `TOUGH_EGG` 16/16 block=0 → Unknown  powers=[HATCH_POWER:1,MINION_POWER:1]
    - [2] `TOUGH_EGG` 15/15 block=0 → Unknown  powers=[HATCH_POWER:1,MINION_POWER:1]
    - [3] `TOUGH_EGG` 21/21 block=0 → Attack 4×1  powers=[MINION_POWER:1]
    - [4] `OVICOPTER` 113/125 block=0 → Attack 16×1  powers=[STRENGTH_POWER:3]
  - player powers: VULNERABLE_POWER:4

  → play card [0] target=0
  → play card [0] target=0
  → end_turn → round transition
  - rewards offered:
    - [0] gold 12g  canSkip=False
    - [1] potion potion=`POTION.SPEED_POTION (62644428)`  canSkip=False
    - [2] card cards=[`BloodWall`(cost=2), `Bloodletting`(cost=0), `Breakthrough`(cost=1)]  canSkip=True

  - rewards offered:
    - [0] gold 12g  canSkip=False
    - [1] potion potion=`POTION.SPEED_POTION (62644428)`  canSkip=False
    - [2] card cards=[`BloodWall`(cost=2), `Bloodletting`(cost=0), `Breakthrough`(cost=1)]  canSkip=True

  - heal → hp=80/80

  - rewards offered:
    - [0] gold 12g  canSkip=False
    - [1] potion potion=`POTION.SPEED_POTION (62644428)`  canSkip=False
    - [2] card cards=[`BloodWall`(cost=2), `Bloodletting`(cost=0), `Breakthrough`(cost=1)]  canSkip=True

  → claim reward [0] → hp=80 room=CombatRoom
  - rewards offered:
    - [0] potion potion=`POTION.SPEED_POTION (62644428)`  canSkip=False
    - [1] card cards=[`BloodWall`(cost=2), `Bloodletting`(cost=0), `Breakthrough`(cost=1)]  canSkip=True

  - rewards offered:
    - [0] potion potion=`POTION.SPEED_POTION (62644428)`  canSkip=False
    - [1] card cards=[`BloodWall`(cost=2), `Bloodletting`(cost=0), `Breakthrough`(cost=1)]  canSkip=True

  → claim reward [0] → hp=80 room=CombatRoom
  - rewards offered:
    - [0] card cards=[`BloodWall`(cost=2), `Bloodletting`(cost=0), `Breakthrough`(cost=1)]  canSkip=True

  - rewards offered:
    - [0] card cards=[`BloodWall`(cost=2), `Bloodletting`(cost=0), `Breakthrough`(cost=1)]  canSkip=True

## Floor 14: MapRoom  (hp=80)

  - map options: (1,14):RestSite

  → skip reward [0] → hp=80 room=MapRoom
  - combat ended (hp=80)

## Floor 15: RestSiteRoom  (hp=80)


  → pick map (1,14) → RestSiteRoom floor=15
## Floor 15: MapRoom  (hp=80)

  - map options: (3,15):Boss

  → pick rest option [0] → MapRoom hp=80
## Floor 16: BossRoom  (hp=80)


  → pick map (3,15) → BossRoom floor=16
### Combat #1 on floor 16

#### Round 1  (e=3/3 block=0 draw=5 disc=0)

  - hand:
    - [0] `DefendIronclad` cost=1 canPlay=True target=Self
    - [1] `Bash` cost=2 canPlay=True target=AnyEnemy
    - [2] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [3] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [4] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `KNOWLEDGE_DEMON` 379/379 block=0 → Debuff

  → play card [0]
  → play card [0] target=0
  → end_turn → round transition
#### Round 2  (e=3/3 block=0 draw=0 disc=5)

  - hand:
    - [0] `DefendIronclad` cost=1 canPlay=True target=Self
    - [1] `DefendIronclad` cost=1 canPlay=True target=Self
    - [2] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [3] `DefendIronclad` cost=1 canPlay=True target=Self
    - [4] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `KNOWLEDGE_DEMON` 371/379 block=0 → Attack 17×1  powers=[VULNERABLE_POWER:1]

  → play card [0]
  → play card [0]
  → play card [0] target=0
  → end_turn → round transition
#### Round 3  (e=3/3 block=0 draw=5 disc=0)

  - hand:
    - [0] `DefendIronclad` cost=1 canPlay=True target=Self
    - [1] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [2] `DefendIronclad` cost=1 canPlay=True target=Self
    - [3] `DefendIronclad` cost=1 canPlay=True target=Self
    - [4] `DefendIronclad` cost=1 canPlay=True target=Self
  - enemies:
    - [0] `KNOWLEDGE_DEMON` 362/379 block=0 → Attack 8×3

  → play card [0]
  → play card [0] target=0
  → play card [0]
  → end_turn → round transition
#### Round 4  (e=3/3 block=0 draw=0 disc=5)

  - hand:
    - [0] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [1] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [2] `Bash` cost=2 canPlay=True target=AnyEnemy
    - [3] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [4] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `KNOWLEDGE_DEMON` 356/379 block=0 → Attack 11×1 + Unknown + Buff

  → play card [0] target=0
  → play card [0] target=0
  → play card [1] target=0
  → end_turn → round transition
#### Round 5  (e=3/3 block=0 draw=5 disc=0)

  - hand:
    - [0] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [1] `DefendIronclad` cost=1 canPlay=True target=Self
    - [2] `Bash` cost=2 canPlay=True target=AnyEnemy
    - [3] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [4] `DefendIronclad` cost=1 canPlay=True target=Self
  - enemies:
    - [0] `KNOWLEDGE_DEMON` 368/379 block=0 → Debuff  powers=[STRENGTH_POWER:2]

  → play card [0] target=0
  → play card [0]
  → play card [1] target=0
  → end_turn → round transition
#### Round 6  (e=3/3 block=0 draw=0 disc=5)

  - hand:
    - [0] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [1] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [2] `DefendIronclad` cost=1 canPlay=True target=Self
    - [3] `DefendIronclad` cost=1 canPlay=True target=Self
    - [4] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `KNOWLEDGE_DEMON` 356/379 block=0 → Attack 17×1  powers=[STRENGTH_POWER:2]

  → play card [0] target=0
  → play card [0] target=0
  → play card [0]
  → end_turn → round transition
#### Round 7  (e=3/3 block=0 draw=5 disc=0)

  - hand:
    - [0] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [1] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [2] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [3] `DefendIronclad` cost=1 canPlay=True target=Self
    - [4] `DefendIronclad` cost=1 canPlay=True target=Self
  - enemies:
    - [0] `KNOWLEDGE_DEMON` 344/379 block=0 → Attack 8×3  powers=[STRENGTH_POWER:2]

  → play card [0] target=0
  → play card [0] target=0
  → play card [0] target=0
  → end_turn → round transition
#### Round 8  (e=3/3 block=0 draw=0 disc=5)

  - hand:
    - [0] `DefendIronclad` cost=1 canPlay=True target=Self
    - [1] `DefendIronclad` cost=1 canPlay=True target=Self
    - [2] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [3] `Bash` cost=2 canPlay=True target=AnyEnemy
    - [4] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `KNOWLEDGE_DEMON` 326/379 block=0 → Attack 11×1 + Unknown + Buff  powers=[STRENGTH_POWER:2]

  → play card [0]
  → play card [0]
  → play card [0] target=0
  → end_turn → round transition
#### Round 9  (e=3/3 block=0 draw=5 disc=0)

  - hand:
    - [0] `Bash` cost=2 canPlay=True target=AnyEnemy
    - [1] `DefendIronclad` cost=1 canPlay=True target=Self
    - [2] `DefendIronclad` cost=1 canPlay=True target=Self
    - [3] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [4] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `KNOWLEDGE_DEMON` 350/379 block=0 → Debuff  powers=[STRENGTH_POWER:4]

  → play card [0] target=0
  → play card [0]
  → end_turn → round transition
#### Round 10  (e=3/3 block=0 draw=0 disc=5)

  - hand:
    - [0] `DefendIronclad` cost=1 canPlay=True target=Self
    - [1] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [2] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [3] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [4] `DefendIronclad` cost=1 canPlay=True target=Self
  - enemies:
    - [0] `KNOWLEDGE_DEMON` 342/379 block=0 → Attack 17×1  powers=[STRENGTH_POWER:4,VULNERABLE_POWER:1]

  → play card [0]
  → play card [0] target=0
  → play card [0] target=0
  → end_turn → round transition
  - rewards offered:
    - [0] gold 100g  canSkip=False
    - [1] card cards=[`OneTwoPunch`(cost=1), `TearAsunder`(cost=2), `Cascade`(cost=0)]  canSkip=True

  - rewards offered:
    - [0] gold 100g  canSkip=False
    - [1] card cards=[`OneTwoPunch`(cost=1), `TearAsunder`(cost=2), `Cascade`(cost=0)]  canSkip=True

  - heal → hp=80/80

  - rewards offered:
    - [0] gold 100g  canSkip=False
    - [1] card cards=[`OneTwoPunch`(cost=1), `TearAsunder`(cost=2), `Cascade`(cost=0)]  canSkip=True

  → claim reward [0] → hp=80 room=BossRoom
  - rewards offered:
    - [0] card cards=[`OneTwoPunch`(cost=1), `TearAsunder`(cost=2), `Cascade`(cost=0)]  canSkip=True

  - rewards offered:
    - [0] card cards=[`OneTwoPunch`(cost=1), `TearAsunder`(cost=2), `Cascade`(cost=0)]  canSkip=True

## Floor 16: MapRoom  (hp=80)


  → skip reward [0] → hp=80 room=MapRoom
  - combat ended (hp=80)

## Floor 0: MapRoom  (hp=80)

  - map options: (3,0):Unknown, (1,1):Monster, (2,1):Monster, (4,1):Monster

  → enter_next_act → MapRoom floor=0
## Floor 2: CombatRoom  (hp=80)


  → pick map (1,1) → CombatRoom floor=2
### Combat #1 on floor 2

#### Round 1  (e=3/3 block=0 draw=5 disc=0)

  - hand:
    - [0] `DefendIronclad` cost=1 canPlay=True target=Self
    - [1] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [2] `DefendIronclad` cost=1 canPlay=True target=Self
    - [3] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [4] `DefendIronclad` cost=1 canPlay=True target=Self
  - enemies:
    - [0] `LIVING_SHIELD` 55/55 block=0 → Attack 6×1  powers=[RAMPART_POWER:25]
    - [1] `TURRET_OPERATOR` 41/41 block=25 → Attack 3×5

  → play card [0]
  → play card [0] target=0
  → play card [0]
  → end_turn → round transition
#### Round 2  (e=3/3 block=0 draw=0 disc=5)

  - hand:
    - [0] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [1] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [2] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [3] `DefendIronclad` cost=1 canPlay=True target=Self
    - [4] `Bash` cost=2 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `LIVING_SHIELD` 49/55 block=0 → Attack 6×1  powers=[RAMPART_POWER:25]
    - [1] `TURRET_OPERATOR` 41/41 block=25 → Attack 3×5

  → play card [0] target=0
  → play card [0] target=0
  → play card [0] target=0
  → end_turn → round transition
#### Round 3  (e=3/3 block=0 draw=5 disc=0)

  - hand:
    - [0] `DefendIronclad` cost=1 canPlay=True target=Self
    - [1] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [2] `DefendIronclad` cost=1 canPlay=True target=Self
    - [3] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [4] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `LIVING_SHIELD` 31/55 block=0 → Attack 6×1  powers=[RAMPART_POWER:25]
    - [1] `TURRET_OPERATOR` 41/41 block=25 → Buff

  → play card [0]
  → play card [0] target=0
  → play card [0]
  → end_turn → round transition
#### Round 4  (e=3/3 block=0 draw=0 disc=5)

  - hand:
    - [0] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [1] `Bash` cost=2 canPlay=True target=AnyEnemy
    - [2] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [3] `DefendIronclad` cost=1 canPlay=True target=Self
    - [4] `DefendIronclad` cost=1 canPlay=True target=Self
  - enemies:
    - [0] `LIVING_SHIELD` 25/55 block=0 → Attack 6×1  powers=[RAMPART_POWER:25]
    - [1] `TURRET_OPERATOR` 41/41 block=25 → Attack 3×5  powers=[STRENGTH_POWER:1]

  → play card [0] target=0
  → play card [0] target=0
  → end_turn → round transition
#### Round 5  (e=3/3 block=0 draw=5 disc=0)

  - hand:
    - [0] `Bash` cost=2 canPlay=True target=AnyEnemy
    - [1] `DefendIronclad` cost=1 canPlay=True target=Self
    - [2] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [3] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [4] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `LIVING_SHIELD` 11/55 block=0 → Attack 6×1  powers=[RAMPART_POWER:25,VULNERABLE_POWER:1]
    - [1] `TURRET_OPERATOR` 41/41 block=25 → Attack 3×5  powers=[STRENGTH_POWER:1]

  → play card [0] target=0
  → play card [0]
  → end_turn → round transition
#### Round 6  (e=3/3 block=0 draw=0 disc=5)

  - hand:
    - [0] `DefendIronclad` cost=1 canPlay=True target=Self
    - [1] `DefendIronclad` cost=1 canPlay=True target=Self
    - [2] `DefendIronclad` cost=1 canPlay=True target=Self
    - [3] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [4] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `TURRET_OPERATOR` 41/41 block=0 → Buff  powers=[STRENGTH_POWER:1]

  → play card [0]
  → play card [0]
  → play card [0]
  → end_turn → round transition
#### Round 7  (e=3/3 block=0 draw=5 disc=0)

  - hand:
    - [0] `Bash` cost=2 canPlay=True target=AnyEnemy
    - [1] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [2] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [3] `DefendIronclad` cost=1 canPlay=True target=Self
    - [4] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `TURRET_OPERATOR` 41/41 block=0 → Attack 3×5  powers=[STRENGTH_POWER:2]

  → play card [0] target=0
  → play card [0] target=0
  → end_turn → round transition
  - rewards offered:
    - [0] gold 10g  canSkip=False
    - [1] card cards=[`IronWave`(cost=1), `MoltenFist`(cost=1), `Tremble`(cost=1)]  canSkip=True

  - rewards offered:
    - [0] gold 10g  canSkip=False
    - [1] card cards=[`IronWave`(cost=1), `MoltenFist`(cost=1), `Tremble`(cost=1)]  canSkip=True

  - heal → hp=80/80

  - rewards offered:
    - [0] gold 10g  canSkip=False
    - [1] card cards=[`IronWave`(cost=1), `MoltenFist`(cost=1), `Tremble`(cost=1)]  canSkip=True

  → claim reward [0] → hp=80 room=CombatRoom
  - rewards offered:
    - [0] card cards=[`IronWave`(cost=1), `MoltenFist`(cost=1), `Tremble`(cost=1)]  canSkip=True

  - rewards offered:
    - [0] card cards=[`IronWave`(cost=1), `MoltenFist`(cost=1), `Tremble`(cost=1)]  canSkip=True

## Floor 2: MapRoom  (hp=80)

  - map options: (0,2):Unknown

  → skip reward [0] → hp=80 room=MapRoom
  - combat ended (hp=80)

## Floor 3: MerchantRoom  (hp=80)


  → pick map (0,2) → MerchantRoom floor=3
## Floor 3: MapRoom  (hp=80)

  - map options: (0,3):Monster

  → leave merchant → MapRoom
## Floor 4: CombatRoom  (hp=80)


  → pick map (0,3) → CombatRoom floor=4
### Combat #1 on floor 4

#### Round 1  (e=3/3 block=0 draw=5 disc=0)

  - hand:
    - [0] `DefendIronclad` cost=1 canPlay=True target=Self
    - [1] `DefendIronclad` cost=1 canPlay=True target=Self
    - [2] `Bash` cost=2 canPlay=True target=AnyEnemy
    - [3] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [4] `DefendIronclad` cost=1 canPlay=True target=Self
  - enemies:
    - [0] `DEVOTED_SCULPTOR` 162/162 block=0 → Buff

  → play card [0]
  → play card [0]
  → play card [1] target=0
  → end_turn → round transition
#### Round 2  (e=3/3 block=0 draw=0 disc=5)

  - hand:
    - [0] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [1] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [2] `DefendIronclad` cost=1 canPlay=True target=Self
    - [3] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [4] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `DEVOTED_SCULPTOR` 156/162 block=0 → Attack 12×1  powers=[RITUAL_POWER:9]

  → play card [0] target=0
  → play card [0] target=0
  → play card [0]
  → end_turn → round transition
#### Round 3  (e=3/3 block=0 draw=5 disc=0)

  - hand:
    - [0] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [1] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [2] `DefendIronclad` cost=1 canPlay=True target=Self
    - [3] `Bash` cost=2 canPlay=True target=AnyEnemy
    - [4] `DefendIronclad` cost=1 canPlay=True target=Self
  - enemies:
    - [0] `DEVOTED_SCULPTOR` 144/162 block=0 → Attack 12×1  powers=[RITUAL_POWER:9,STRENGTH_POWER:9]

  → play card [0] target=0
  → play card [0] target=0
  → play card [0]
  → end_turn → round transition
#### Round 4  (e=3/3 block=0 draw=0 disc=5)

  - hand:
    - [0] `DefendIronclad` cost=1 canPlay=True target=Self
    - [1] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [2] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [3] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [4] `DefendIronclad` cost=1 canPlay=True target=Self
  - enemies:
    - [0] `DEVOTED_SCULPTOR` 132/162 block=0 → Attack 12×1  powers=[RITUAL_POWER:9,STRENGTH_POWER:18]

  → play card [0]
  → play card [0] target=0
  → play card [0] target=0
  → end_turn → round transition
#### Round 5  (e=3/3 block=0 draw=5 disc=0)

  - hand:
    - [0] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [1] `DefendIronclad` cost=1 canPlay=True target=Self
    - [2] `DefendIronclad` cost=1 canPlay=True target=Self
    - [3] `DefendIronclad` cost=1 canPlay=True target=Self
    - [4] `DefendIronclad` cost=1 canPlay=True target=Self
  - enemies:
    - [0] `DEVOTED_SCULPTOR` 120/162 block=0 → Attack 12×1  powers=[RITUAL_POWER:9,STRENGTH_POWER:27]

  → play card [0] target=0
  → play card [0]
  → play card [0]
  → end_turn → round transition
#### Round 6  (e=3/3 block=0 draw=0 disc=5)

  - hand:
    - [0] `Bash` cost=2 canPlay=True target=AnyEnemy
    - [1] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [2] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [3] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [4] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `DEVOTED_SCULPTOR` 114/162 block=0 → Attack 12×1  powers=[RITUAL_POWER:9,STRENGTH_POWER:36]

  → play card [0] target=0
  → play card [0] target=0
  → end_turn → round transition
  - rewards offered:
    - [0] gold 18g  canSkip=False
    - [1] potion potion=`POTION.BEETLE_JUICE (8866062)`  canSkip=False
    - [2] card cards=[`Tremble`(cost=1), `ForgottenRitual`(cost=1), `Havoc`(cost=1)]  canSkip=True

  - rewards offered:
    - [0] gold 18g  canSkip=False
    - [1] potion potion=`POTION.BEETLE_JUICE (8866062)`  canSkip=False
    - [2] card cards=[`Tremble`(cost=1), `ForgottenRitual`(cost=1), `Havoc`(cost=1)]  canSkip=True

  - heal → hp=80/80

  - rewards offered:
    - [0] gold 18g  canSkip=False
    - [1] potion potion=`POTION.BEETLE_JUICE (8866062)`  canSkip=False
    - [2] card cards=[`Tremble`(cost=1), `ForgottenRitual`(cost=1), `Havoc`(cost=1)]  canSkip=True

  → claim reward [0] → hp=80 room=CombatRoom
  - rewards offered:
    - [0] potion potion=`POTION.BEETLE_JUICE (8866062)`  canSkip=False
    - [1] card cards=[`Tremble`(cost=1), `ForgottenRitual`(cost=1), `Havoc`(cost=1)]  canSkip=True

  - rewards offered:
    - [0] potion potion=`POTION.BEETLE_JUICE (8866062)`  canSkip=False
    - [1] card cards=[`Tremble`(cost=1), `ForgottenRitual`(cost=1), `Havoc`(cost=1)]  canSkip=True

  → claim reward [0] → hp=80 room=CombatRoom
  - rewards offered:
    - [0] card cards=[`Tremble`(cost=1), `ForgottenRitual`(cost=1), `Havoc`(cost=1)]  canSkip=True

  - rewards offered:
    - [0] card cards=[`Tremble`(cost=1), `ForgottenRitual`(cost=1), `Havoc`(cost=1)]  canSkip=True

## Floor 4: MapRoom  (hp=80)

  - map options: (0,4):Monster

  → skip reward [0] → hp=80 room=MapRoom
  - combat ended (hp=80)

## Floor 5: CombatRoom  (hp=80)


  → pick map (0,4) → CombatRoom floor=5
### Combat #1 on floor 5

#### Round 1  (e=3/3 block=0 draw=5 disc=0)

  - hand:
    - [0] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [1] `Bash` cost=2 canPlay=True target=AnyEnemy
    - [2] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [3] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [4] `DefendIronclad` cost=1 canPlay=True target=Self
  - enemies:
    - [0] `GLOBE_HEAD` 148/148 block=0 → Attack 13×1 + Debuff  powers=[GALVANIC_POWER:6]

  → play card [0] target=0
  → play card [0] target=0
  → end_turn → round transition
#### Round 2  (e=3/3 block=0 draw=0 disc=5)

  - hand:
    - [0] `DefendIronclad` cost=1 canPlay=True target=Self
    - [1] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [2] `DefendIronclad` cost=1 canPlay=True target=Self
    - [3] `DefendIronclad` cost=1 canPlay=True target=Self
    - [4] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `GLOBE_HEAD` 134/148 block=0 → Attack 6×3  powers=[GALVANIC_POWER:6,VULNERABLE_POWER:1]
  - player powers: FRAIL_POWER:2

  → play card [0]
  → play card [0] target=0
  → play card [0]
  → end_turn → round transition
#### Round 3  (e=3/3 block=0 draw=5 disc=0)

  - hand:
    - [0] `DefendIronclad` cost=1 canPlay=True target=Self
    - [1] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [2] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [3] `DefendIronclad` cost=1 canPlay=True target=Self
    - [4] `Bash` cost=2 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `GLOBE_HEAD` 125/148 block=0 → Attack 16×1 + Buff  powers=[GALVANIC_POWER:6]
  - player powers: FRAIL_POWER:2

  → play card [0]
  → play card [0] target=0
  → play card [0] target=0
  → end_turn → round transition
#### Round 4  (e=3/3 block=0 draw=0 disc=5)

  - hand:
    - [0] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [1] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [2] `DefendIronclad` cost=1 canPlay=True target=Self
    - [3] `DefendIronclad` cost=1 canPlay=True target=Self
    - [4] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `GLOBE_HEAD` 113/148 block=0 → Attack 13×1 + Debuff  powers=[GALVANIC_POWER:6,STRENGTH_POWER:2]
  - player powers: FRAIL_POWER:2

  → play card [0] target=0
  → play card [0] target=0
  → play card [0]
  → end_turn → round transition
#### Round 5  (e=3/3 block=0 draw=5 disc=0)

  - hand:
    - [0] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [1] `DefendIronclad` cost=1 canPlay=True target=Self
    - [2] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [3] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [4] `Bash` cost=2 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `GLOBE_HEAD` 101/148 block=0 → Attack 6×3  powers=[GALVANIC_POWER:6,STRENGTH_POWER:2]
  - player powers: FRAIL_POWER:4

  → play card [0] target=0
  → play card [0]
  → play card [0] target=0
  → end_turn → round transition
#### Round 6  (e=3/3 block=0 draw=0 disc=5)

  - hand:
    - [0] `DefendIronclad` cost=1 canPlay=True target=Self
    - [1] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [2] `DefendIronclad` cost=1 canPlay=True target=Self
    - [3] `DefendIronclad` cost=1 canPlay=True target=Self
    - [4] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `GLOBE_HEAD` 89/148 block=0 → Attack 16×1 + Buff  powers=[GALVANIC_POWER:6,STRENGTH_POWER:2]
  - player powers: FRAIL_POWER:4

  → play card [0]
  → play card [0] target=0
  → play card [0]
  → end_turn → round transition
#### Round 7  (e=3/3 block=0 draw=5 disc=0)

  - hand:
    - [0] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [1] `DefendIronclad` cost=1 canPlay=True target=Self
    - [2] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [3] `DefendIronclad` cost=1 canPlay=True target=Self
    - [4] `DefendIronclad` cost=1 canPlay=True target=Self
  - enemies:
    - [0] `GLOBE_HEAD` 83/148 block=0 → Attack 13×1 + Debuff  powers=[GALVANIC_POWER:6,STRENGTH_POWER:4]
  - player powers: FRAIL_POWER:4

  → play card [0] target=0
  → play card [0]
  → play card [0] target=0
  → end_turn → round transition
  - rewards offered:
    - [0] gold 16g  canSkip=False
    - [1] card cards=[`InfernalBlade`(cost=1), `BloodWall`(cost=2), `MoltenFist`(cost=1)]  canSkip=True

  - rewards offered:
    - [0] gold 16g  canSkip=False
    - [1] card cards=[`InfernalBlade`(cost=1), `BloodWall`(cost=2), `MoltenFist`(cost=1)]  canSkip=True

  - heal → hp=80/80

  - rewards offered:
    - [0] gold 16g  canSkip=False
    - [1] card cards=[`InfernalBlade`(cost=1), `BloodWall`(cost=2), `MoltenFist`(cost=1)]  canSkip=True

  → claim reward [0] → hp=80 room=CombatRoom
  - rewards offered:
    - [0] card cards=[`InfernalBlade`(cost=1), `BloodWall`(cost=2), `MoltenFist`(cost=1)]  canSkip=True

  - rewards offered:
    - [0] card cards=[`InfernalBlade`(cost=1), `BloodWall`(cost=2), `MoltenFist`(cost=1)]  canSkip=True

## Floor 5: MapRoom  (hp=80)

  - map options: (1,5):Unknown

  → skip reward [0] → hp=80 room=MapRoom
  - combat ended (hp=80)

## Floor 6: EventRoom  (hp=80)

  - event options:
    - [0] `CRYSTAL_SPHERE.pages.INITIAL.options.UNCOVER_FUTURE`
    - [1] `CRYSTAL_SPHERE.pages.INITIAL.options.PAYMENT_PLAN`

  → pick map (1,5) → EventRoom floor=6
## Floor 6: MapRoom  (hp=80)

  - map options: (1,6):Elite

  → pick event option [1] → MapRoom hp=80
## Floor 7: CombatRoom  (hp=80)


  → pick map (1,6) → CombatRoom floor=7
### Combat #1 on floor 7

#### Round 1  (e=3/3 block=0 draw=6 disc=0)

  - hand:
    - [0] `Bash` cost=2 canPlay=True target=AnyEnemy
    - [1] `DefendIronclad` cost=1 canPlay=True target=Self
    - [2] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [3] `DefendIronclad` cost=1 canPlay=True target=Self
    - [4] `Debt` cost=0 canPlay=False target=None
  - enemies:
    - [0] `SOUL_NEXUS` 234/234 block=0 → Attack 29×1

  → play card [0] target=0
  → play card [0]
  → end_turn → round transition
#### Round 2  (e=3/3 block=0 draw=1 disc=5)

  - hand:
    - [0] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [1] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [2] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [3] `DefendIronclad` cost=1 canPlay=True target=Self
    - [4] `DefendIronclad` cost=1 canPlay=True target=Self
  - enemies:
    - [0] `SOUL_NEXUS` 226/234 block=0 → Attack 18×1 + Unknown  powers=[VULNERABLE_POWER:1]

  → play card [0] target=0
  → play card [0] target=0
  → play card [0] target=0
  → end_turn → round transition
#### Round 3  (e=3/3 block=0 draw=6 disc=0)

  - hand:
    - [0] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [1] `DefendIronclad` cost=1 canPlay=True target=Self
    - [2] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [3] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [4] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `SOUL_NEXUS` 199/234 block=0 → Attack 29×1

  → play card [0] target=0
  → play card [0]
  → play card [0] target=0
  → end_turn → round transition
#### Round 4  (e=3/3 block=0 draw=1 disc=5)

  - hand:
    - [0] `DefendIronclad` cost=1 canPlay=True target=Self
    - [1] `DefendIronclad` cost=1 canPlay=True target=Self
    - [2] `Debt` cost=0 canPlay=False target=None
    - [3] `DefendIronclad` cost=1 canPlay=True target=Self
    - [4] `Bash` cost=2 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `SOUL_NEXUS` 187/234 block=0 → Attack 18×1 + Unknown

  → play card [0]
  → play card [0]
  → play card [1]
  → end_turn → round transition
#### Round 5  (e=3/3 block=0 draw=6 disc=0)

  - hand:
    - [0] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [1] `Debt` cost=0 canPlay=False target=None
    - [2] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [3] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [4] `Bash` cost=2 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `SOUL_NEXUS` 187/234 block=0 → Attack 6×4

  → play card [0] target=0
  → play card [1] target=0
  → play card [1] target=0
  → end_turn → round transition
#### Round 6  (e=3/3 block=0 draw=1 disc=5)

  - hand:
    - [0] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [1] `DefendIronclad` cost=1 canPlay=True target=Self
    - [2] `DefendIronclad` cost=1 canPlay=True target=Self
    - [3] `DefendIronclad` cost=1 canPlay=True target=Self
    - [4] `DefendIronclad` cost=1 canPlay=True target=Self
  - enemies:
    - [0] `SOUL_NEXUS` 169/234 block=0 → Attack 29×1

  → play card [0] target=0
  → play card [0]
  → play card [0]
  → end_turn → round transition
#### Round 7  (e=3/3 block=0 draw=6 disc=0)

  - hand:
    - [0] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [1] `DefendIronclad` cost=1 canPlay=True target=Self
    - [2] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [3] `DefendIronclad` cost=1 canPlay=True target=Self
    - [4] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `SOUL_NEXUS` 163/234 block=0 → Attack 18×1 + Unknown

  → play card [0] target=0
  → play card [0]
  → play card [0] target=0
  → end_turn → round transition
#### Round 8  (e=3/3 block=0 draw=1 disc=5)

  - hand:
    - [0] `Bash` cost=2 canPlay=True target=AnyEnemy
    - [1] `Debt` cost=0 canPlay=False target=None
    - [2] `DefendIronclad` cost=1 canPlay=True target=Self
    - [3] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [4] `DefendIronclad` cost=1 canPlay=True target=Self
  - enemies:
    - [0] `SOUL_NEXUS` 151/234 block=0 → Attack 6×4

  → play card [0] target=0
  → play card [1]
  → end_turn → round transition
#### Round 9  (e=3/3 block=0 draw=6 disc=0)

  - hand:
    - [0] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [1] `Debt` cost=0 canPlay=False target=None
    - [2] `DefendIronclad` cost=1 canPlay=True target=Self
    - [3] `DefendIronclad` cost=1 canPlay=True target=Self
    - [4] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `SOUL_NEXUS` 143/234 block=0 → Attack 18×1 + Unknown  powers=[VULNERABLE_POWER:1]

  → play card [0] target=0
  → play card [1]
  → play card [1]
  → end_turn → round transition
#### Round 10  (e=3/3 block=0 draw=1 disc=5)

  - hand:
    - [0] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [1] `Bash` cost=2 canPlay=True target=AnyEnemy
    - [2] `DefendIronclad` cost=1 canPlay=True target=Self
    - [3] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [4] `DefendIronclad` cost=1 canPlay=True target=Self
  - enemies:
    - [0] `SOUL_NEXUS` 134/234 block=0 → Attack 29×1

  → play card [0] target=0
  → play card [0] target=0
  → end_turn → round transition
#### Round 11  (e=3/3 block=0 draw=6 disc=0)

  - hand:
    - [0] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [1] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [2] `Bash` cost=2 canPlay=True target=AnyEnemy
    - [3] `DefendIronclad` cost=1 canPlay=True target=Self
    - [4] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `SOUL_NEXUS` 120/234 block=0 → Attack 18×1 + Unknown  powers=[VULNERABLE_POWER:1]

  → play card [0] target=0
  → play card [0] target=0
  → play card [1]
  → end_turn → round transition
#### Round 12  (e=3/3 block=0 draw=1 disc=5)

  - hand:
    - [0] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [1] `DefendIronclad` cost=1 canPlay=True target=Self
    - [2] `DefendIronclad` cost=1 canPlay=True target=Self
    - [3] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [4] `DefendIronclad` cost=1 canPlay=True target=Self
  - enemies:
    - [0] `SOUL_NEXUS` 102/234 block=0 → Attack 29×1

  → play card [0] target=0
  → play card [0]
  → play card [0]
  → end_turn → round transition
#### Round 13  (e=3/3 block=0 draw=6 disc=0)

  - hand:
    - [0] `Debt` cost=0 canPlay=False target=None
    - [1] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [2] `DefendIronclad` cost=1 canPlay=True target=Self
    - [3] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [4] `DefendIronclad` cost=1 canPlay=True target=Self
  - enemies:
    - [0] `SOUL_NEXUS` 96/234 block=0 → Attack 6×4

  → play card [1] target=0
  → play card [1]
  → play card [1] target=0
  → end_turn → round transition
#### Round 14  (e=3/3 block=0 draw=1 disc=5)

  - hand:
    - [0] `DefendIronclad` cost=1 canPlay=True target=Self
    - [1] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [2] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [3] `Bash` cost=2 canPlay=True target=AnyEnemy
    - [4] `DefendIronclad` cost=1 canPlay=True target=Self
  - enemies:
    - [0] `SOUL_NEXUS` 84/234 block=0 → Attack 18×1 + Unknown

  → play card [0]
  → play card [0] target=0
  → play card [0] target=0
  → end_turn → round transition
#### Round 15  (e=3/3 block=0 draw=6 disc=0)

  - hand:
    - [0] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [1] `DefendIronclad` cost=1 canPlay=True target=Self
    - [2] `Bash` cost=2 canPlay=True target=AnyEnemy
    - [3] `DefendIronclad` cost=1 canPlay=True target=Self
    - [4] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `SOUL_NEXUS` 72/234 block=0 → Attack 29×1

  → play card [0] target=0
  → play card [0]
  → play card [1]
  → end_turn → round transition
#### Round 16  (e=3/3 block=0 draw=1 disc=5)

  - hand:
    - [0] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [1] `DefendIronclad` cost=1 canPlay=True target=Self
    - [2] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [3] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [4] `Debt` cost=0 canPlay=False target=None
  - enemies:
    - [0] `SOUL_NEXUS` 66/234 block=0 → Attack 6×4

  → play card [0] target=0
  → play card [0]
  → play card [0] target=0
  → end_turn → round transition
#### Round 17  (e=3/3 block=0 draw=6 disc=0)

  - hand:
    - [0] `DefendIronclad` cost=1 canPlay=True target=Self
    - [1] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [2] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [3] `DefendIronclad` cost=1 canPlay=True target=Self
    - [4] `Debt` cost=0 canPlay=False target=None
  - enemies:
    - [0] `SOUL_NEXUS` 54/234 block=0 → Attack 29×1

  → play card [0]
  → play card [0] target=0
  → play card [0] target=0
  → end_turn → round transition
#### Round 18  (e=3/3 block=0 draw=1 disc=5)

  - hand:
    - [0] `DefendIronclad` cost=1 canPlay=True target=Self
    - [1] `Bash` cost=2 canPlay=True target=AnyEnemy
    - [2] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [3] `DefendIronclad` cost=1 canPlay=True target=Self
    - [4] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `SOUL_NEXUS` 42/234 block=0 → Attack 6×4

  → play card [0]
  → play card [0] target=0
  → end_turn → round transition
#### Round 19  (e=3/3 block=0 draw=6 disc=0)

  - hand:
    - [0] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [1] `DefendIronclad` cost=1 canPlay=True target=Self
    - [2] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [3] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [4] `DefendIronclad` cost=1 canPlay=True target=Self
  - enemies:
    - [0] `SOUL_NEXUS` 34/234 block=0 → Attack 29×1  powers=[VULNERABLE_POWER:1]

  → play card [0] target=0
  → play card [0]
  → play card [0] target=0
  → end_turn → round transition
#### Round 20  (e=3/3 block=0 draw=1 disc=5)

  - hand:
    - [0] `Debt` cost=0 canPlay=False target=None
    - [1] `DefendIronclad` cost=1 canPlay=True target=Self
    - [2] `DefendIronclad` cost=1 canPlay=True target=Self
    - [3] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [4] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `SOUL_NEXUS` 16/234 block=0 → Attack 6×4

  → play card [1]
  → play card [1]
  → play card [1] target=0
  → end_turn → round transition
#### Round 21  (e=3/3 block=0 draw=6 disc=0)

  - hand:
    - [0] `Bash` cost=2 canPlay=True target=AnyEnemy
    - [1] `Debt` cost=0 canPlay=False target=None
    - [2] `DefendIronclad` cost=1 canPlay=True target=Self
    - [3] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [4] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `SOUL_NEXUS` 10/234 block=0 → Attack 18×1 + Unknown

  → play card [0] target=0
  → play card [1]
  → end_turn → round transition
#### Round 22  (e=3/3 block=0 draw=1 disc=5)

  - hand:
    - [0] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [1] `DefendIronclad` cost=1 canPlay=True target=Self
    - [2] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [3] `DefendIronclad` cost=1 canPlay=True target=Self
    - [4] `DefendIronclad` cost=1 canPlay=True target=Self
  - enemies:
    - [0] `SOUL_NEXUS` 2/234 block=0 → Attack 6×4  powers=[VULNERABLE_POWER:1]

  → play card [0] target=0
  - rewards offered:
    - [0] gold 43g  canSkip=False
    - [1] potion potion=`POTION.OROBIC_ACID (63183853)`  canSkip=False
    - [2] relic relic=``  canSkip=False
    - [3] card cards=[`Havoc`(cost=1), `Pillage`(cost=1), `SwordBoomerang`(cost=1)]  canSkip=True

  - rewards offered:
    - [0] gold 43g  canSkip=False
    - [1] potion potion=`POTION.OROBIC_ACID (63183853)`  canSkip=False
    - [2] relic relic=``  canSkip=False
    - [3] card cards=[`Havoc`(cost=1), `Pillage`(cost=1), `SwordBoomerang`(cost=1)]  canSkip=True

  → claim reward [0] → hp=80 room=CombatRoom
  - rewards offered:
    - [0] potion potion=`POTION.OROBIC_ACID (63183853)`  canSkip=False
    - [1] relic relic=``  canSkip=False
    - [2] card cards=[`Havoc`(cost=1), `Pillage`(cost=1), `SwordBoomerang`(cost=1)]  canSkip=True

  - rewards offered:
    - [0] potion potion=`POTION.OROBIC_ACID (63183853)`  canSkip=False
    - [1] relic relic=``  canSkip=False
    - [2] card cards=[`Havoc`(cost=1), `Pillage`(cost=1), `SwordBoomerang`(cost=1)]  canSkip=True

  → claim reward [0] → hp=80 room=CombatRoom
  - rewards offered:
    - [0] relic relic=``  canSkip=False
    - [1] card cards=[`Havoc`(cost=1), `Pillage`(cost=1), `SwordBoomerang`(cost=1)]  canSkip=True

  - rewards offered:
    - [0] relic relic=``  canSkip=False
    - [1] card cards=[`Havoc`(cost=1), `Pillage`(cost=1), `SwordBoomerang`(cost=1)]  canSkip=True

  → claim reward [0] → hp=80 room=CombatRoom
  - rewards offered:
    - [0] card cards=[`Havoc`(cost=1), `Pillage`(cost=1), `SwordBoomerang`(cost=1)]  canSkip=True

  - rewards offered:
    - [0] card cards=[`Havoc`(cost=1), `Pillage`(cost=1), `SwordBoomerang`(cost=1)]  canSkip=True

## Floor 7: MapRoom  (hp=80)

  - map options: (2,7):Treasure

  → skip reward [0] → hp=80 room=MapRoom
  - combat ended (hp=80)

## Floor 8: TreasureRoom  (hp=80)


  → pick map (2,7) → TreasureRoom floor=8
## Floor 8: MapRoom  (hp=80)

  - map options: (1,8):Unknown, (3,8):Elite

  → leave treasure → MapRoom relics=`BURNING_BLOOD`, `ODDLY_SMOOTH_STONE`, `GORGET`, `JUZU_BRACELET`, `TEA_OF_DISCOURTESY`, `ANCHOR`, `MEAL_TICKET`
## Floor 9: CombatRoom  (hp=80)


  → pick map (3,8) → CombatRoom floor=9
### Combat #1 on floor 9

#### Round 1  (e=3/3 block=0 draw=6 disc=0)

  - hand:
    - [0] `DefendIronclad` cost=1 canPlay=True target=Self
    - [1] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [2] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [3] `Debt` cost=0 canPlay=False target=None
    - [4] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `MECHA_KNIGHT` 300/300 block=0 → Attack 25×1  powers=[ARTIFACT_POWER:3]

  → play card [0]
  → play card [0] target=0
  → play card [0] target=0
  → end_turn → round transition
#### Round 2  (e=3/3 block=0 draw=1 disc=5)

  - hand:
    - [0] `DefendIronclad` cost=1 canPlay=True target=Self
    - [1] `Bash` cost=2 canPlay=True target=AnyEnemy
    - [2] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [3] `DefendIronclad` cost=1 canPlay=True target=Self
    - [4] `DefendIronclad` cost=1 canPlay=True target=Self
  - enemies:
    - [0] `MECHA_KNIGHT` 288/300 block=0 → Unknown  powers=[ARTIFACT_POWER:3]

  → play card [0]
  → play card [0] target=0
  → end_turn → round transition
#### Round 3  (e=3/3 block=0 draw=6 disc=0)

  - hand:
    - [0] `Burn` cost=0 canPlay=False target=None
    - [1] `Burn` cost=0 canPlay=False target=None
    - [2] `Burn` cost=0 canPlay=False target=None
    - [3] `Burn` cost=0 canPlay=False target=None
    - [4] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [5] `DefendIronclad` cost=1 canPlay=True target=Self
    - [6] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [7] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [8] `DefendIronclad` cost=1 canPlay=True target=Self
  - enemies:
    - [0] `MECHA_KNIGHT` 280/300 block=0 → Defend + Buff  powers=[ARTIFACT_POWER:2]

  → play card [4] target=0
  → play card [4]
  → play card [4] target=0
  → end_turn → round transition
#### Round 4  (e=3/3 block=0 draw=1 disc=9)

  - hand:
    - [0] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [1] `Bash` cost=2 canPlay=True target=AnyEnemy
    - [2] `Debt` cost=0 canPlay=False target=None
    - [3] `DefendIronclad` cost=1 canPlay=True target=Self
    - [4] `DefendIronclad` cost=1 canPlay=True target=Self
  - enemies:
    - [0] `MECHA_KNIGHT` 268/300 block=15 → Attack 35×1  powers=[ARTIFACT_POWER:2,STRENGTH_POWER:5]

  → play card [0] target=0
  → play card [0] target=0
  → end_turn → round transition
#### Round 5  (e=3/3 block=0 draw=10 disc=0)

  - hand:
    - [0] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [1] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [2] `Burn` cost=0 canPlay=False target=None
    - [3] `Burn` cost=0 canPlay=False target=None
    - [4] `DefendIronclad` cost=1 canPlay=True target=Self
  - enemies:
    - [0] `MECHA_KNIGHT` 268/300 block=0 → Unknown  powers=[ARTIFACT_POWER:1,STRENGTH_POWER:5]

  → play card [0] target=0
  → play card [0] target=0
  → play card [2]
  → end_turn → round transition
#### Round 6  (e=3/3 block=0 draw=5 disc=5)

  - hand:
    - [0] `Burn` cost=0 canPlay=False target=None
    - [1] `Burn` cost=0 canPlay=False target=None
    - [2] `Burn` cost=0 canPlay=False target=None
    - [3] `Burn` cost=0 canPlay=False target=None
    - [4] `Bash` cost=2 canPlay=True target=AnyEnemy
    - [5] `Debt` cost=0 canPlay=False target=None
    - [6] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [7] `Burn` cost=0 canPlay=False target=None
    - [8] `Burn` cost=0 canPlay=False target=None
  - enemies:
    - [0] `MECHA_KNIGHT` 256/300 block=0 → Defend + Buff  powers=[ARTIFACT_POWER:1,STRENGTH_POWER:5]

  → play card [4] target=0
  → play card [5] target=0
  → end_turn → round transition
#### Round 7  (e=3/3 block=0 draw=0 disc=14)

  - hand:
    - [0] `DefendIronclad` cost=1 canPlay=True target=Self
    - [1] `DefendIronclad` cost=1 canPlay=True target=Self
    - [2] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [3] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [4] `DefendIronclad` cost=1 canPlay=True target=Self
  - enemies:
    - [0] `MECHA_KNIGHT` 242/300 block=15 → Attack 35×1  powers=[STRENGTH_POWER:10]

  → play card [0]
  → play card [0]
  → play card [0] target=0
  → end_turn → round transition
  - rewards offered:
    - [0] gold 41g  canSkip=False
    - [1] relic relic=``  canSkip=False
    - [2] card cards=[`Stomp`(cost=3), `Conflagration`(cost=1), `Armaments`(cost=1)]  canSkip=True

  - rewards offered:
    - [0] gold 41g  canSkip=False
    - [1] relic relic=``  canSkip=False
    - [2] card cards=[`Stomp`(cost=3), `Conflagration`(cost=1), `Armaments`(cost=1)]  canSkip=True

  - heal → hp=80/80

  - rewards offered:
    - [0] gold 41g  canSkip=False
    - [1] relic relic=``  canSkip=False
    - [2] card cards=[`Stomp`(cost=3), `Conflagration`(cost=1), `Armaments`(cost=1)]  canSkip=True

  → claim reward [0] → hp=80 room=CombatRoom
  - rewards offered:
    - [0] relic relic=``  canSkip=False
    - [1] card cards=[`Stomp`(cost=3), `Conflagration`(cost=1), `Armaments`(cost=1)]  canSkip=True

  - rewards offered:
    - [0] relic relic=``  canSkip=False
    - [1] card cards=[`Stomp`(cost=3), `Conflagration`(cost=1), `Armaments`(cost=1)]  canSkip=True

  → claim reward [0] → hp=80 room=CombatRoom
  - rewards offered:
    - [0] card cards=[`Stomp`(cost=3), `Conflagration`(cost=1), `Armaments`(cost=1)]  canSkip=True

  - rewards offered:
    - [0] card cards=[`Stomp`(cost=3), `Conflagration`(cost=1), `Armaments`(cost=1)]  canSkip=True

## Floor 9: MapRoom  (hp=80)

  - map options: (4,9):Unknown, (3,9):RestSite

  → skip reward [0] → hp=80 room=MapRoom
  - combat ended (hp=80)

## Floor 10: EventRoom  (hp=80)


  → pick map (4,9) → EventRoom floor=10
## Floor -2: Unknown  (hp=-2)


## Floor 10: MapRoom  (hp=80/80)

  - map options: (5,10):Monster

## Floor 11: CombatRoom  (hp=80)


  → pick map (5,10) → CombatRoom floor=11
### Combat #1 on floor 11

#### Round 1  (e=3/3 block=0 draw=6 disc=0)

  - hand:
    - [0] `DefendIronclad` cost=1 canPlay=True target=Self
    - [1] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [2] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [3] `Debt` cost=0 canPlay=False target=None
    - [4] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `PUNCH_CONSTRUCT` 55/55 block=0 → Defend  powers=[ARTIFACT_POWER:1]
    - [1] `CUBEX_CONSTRUCT` 65/65 block=0 → Buff  powers=[ARTIFACT_POWER:1]
    - [2] `CUBEX_CONSTRUCT` 65/65 block=0 → Buff  powers=[ARTIFACT_POWER:1]

  → play card [0]
  → play card [0] target=0
  → play card [0] target=0
  → end_turn → round transition
#### Round 2  (e=3/3 block=0 draw=1 disc=5)

  - hand:
    - [0] `Bash` cost=2 canPlay=True target=AnyEnemy
    - [1] `DefendIronclad` cost=1 canPlay=True target=Self
    - [2] `DefendIronclad` cost=1 canPlay=True target=Self
    - [3] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [4] `DefendIronclad` cost=1 canPlay=True target=Self
  - enemies:
    - [0] `PUNCH_CONSTRUCT` 43/55 block=10 → Attack 14×1  powers=[ARTIFACT_POWER:1]
    - [1] `CUBEX_CONSTRUCT` 65/65 block=0 → Attack 7×1 + Buff  powers=[ARTIFACT_POWER:1,STRENGTH_POWER:2]
    - [2] `CUBEX_CONSTRUCT` 65/65 block=0 → Attack 7×1 + Buff  powers=[ARTIFACT_POWER:1,STRENGTH_POWER:2]

  → play card [0] target=0
  → play card [0]
  → end_turn → round transition
#### Round 3  (e=3/3 block=0 draw=6 disc=0)

  - hand:
    - [0] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [1] `DefendIronclad` cost=1 canPlay=True target=Self
    - [2] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [3] `DefendIronclad` cost=1 canPlay=True target=Self
    - [4] `Debt` cost=0 canPlay=False target=None
  - enemies:
    - [0] `PUNCH_CONSTRUCT` 43/55 block=0 → Attack 5×2 + Debuff
    - [1] `CUBEX_CONSTRUCT` 65/65 block=0 → Attack 7×1 + Buff  powers=[ARTIFACT_POWER:1,STRENGTH_POWER:4]
    - [2] `CUBEX_CONSTRUCT` 65/65 block=0 → Attack 7×1 + Buff  powers=[ARTIFACT_POWER:1,STRENGTH_POWER:4]

  → play card [0] target=0
  → play card [0]
  → play card [0] target=0
  → end_turn → round transition
#### Round 4  (e=3/3 block=0 draw=1 disc=5)

  - hand:
    - [0] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [1] `Bash` cost=2 canPlay=True target=AnyEnemy
    - [2] `DefendIronclad` cost=1 canPlay=True target=Self
    - [3] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [4] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `PUNCH_CONSTRUCT` 31/55 block=0 → Defend
    - [1] `CUBEX_CONSTRUCT` 65/65 block=0 → Attack 5×2  powers=[ARTIFACT_POWER:1,STRENGTH_POWER:6]
    - [2] `CUBEX_CONSTRUCT` 65/65 block=0 → Attack 5×2  powers=[ARTIFACT_POWER:1,STRENGTH_POWER:6]
  - player powers: WEAK_POWER:1

  → play card [0] target=0
  → play card [0] target=0
  → end_turn → round transition
  - rewards offered:
    - [0] gold 13g  canSkip=False
    - [1] potion potion=`POTION.SHACKLING_POTION (64374149)`  canSkip=False
    - [2] card cards=[`DrumOfBattle`(cost=0), `Tremble`(cost=1), `Headbutt`(cost=1)]  canSkip=True

  - rewards offered:
    - [0] gold 13g  canSkip=False
    - [1] potion potion=`POTION.SHACKLING_POTION (64374149)`  canSkip=False
    - [2] card cards=[`DrumOfBattle`(cost=0), `Tremble`(cost=1), `Headbutt`(cost=1)]  canSkip=True

  - heal → hp=80/80

  - rewards offered:
    - [0] gold 13g  canSkip=False
    - [1] potion potion=`POTION.SHACKLING_POTION (64374149)`  canSkip=False
    - [2] card cards=[`DrumOfBattle`(cost=0), `Tremble`(cost=1), `Headbutt`(cost=1)]  canSkip=True

  → claim reward [0] → hp=80 room=CombatRoom
  - rewards offered:
    - [0] potion potion=`POTION.SHACKLING_POTION (64374149)`  canSkip=False
    - [1] card cards=[`DrumOfBattle`(cost=0), `Tremble`(cost=1), `Headbutt`(cost=1)]  canSkip=True

  - rewards offered:
    - [0] potion potion=`POTION.SHACKLING_POTION (64374149)`  canSkip=False
    - [1] card cards=[`DrumOfBattle`(cost=0), `Tremble`(cost=1), `Headbutt`(cost=1)]  canSkip=True

  → claim reward [0] → hp=80 room=CombatRoom
  - rewards offered:
    - [0] card cards=[`DrumOfBattle`(cost=0), `Tremble`(cost=1), `Headbutt`(cost=1)]  canSkip=True

  - rewards offered:
    - [0] card cards=[`DrumOfBattle`(cost=0), `Tremble`(cost=1), `Headbutt`(cost=1)]  canSkip=True

## Floor 11: MapRoom  (hp=80)

  - map options: (6,11):Unknown, (4,11):Monster

  → skip reward [0] → hp=80 room=MapRoom
  - combat ended (hp=80)

## Floor 12: CombatRoom  (hp=80)


  → pick map (4,11) → CombatRoom floor=12
### Combat #1 on floor 12

#### Round 1  (e=3/3 block=0 draw=6 disc=0)

  - hand:
    - [0] `DefendIronclad` cost=1 canPlay=True target=Self
    - [1] `DefendIronclad` cost=1 canPlay=True target=Self
    - [2] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [3] `DefendIronclad` cost=1 canPlay=True target=Self
    - [4] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `FABRICATOR` 150/150 block=0 → Attack 18×1 + Unknown

  → play card [0]
  → play card [0]
  → play card [0] target=0
  → end_turn → round transition
#### Round 2  (e=3/3 block=0 draw=1 disc=5)

  - hand:
    - [0] `DefendIronclad` cost=1 canPlay=True target=Self
    - [1] `Bash` cost=2 canPlay=True target=AnyEnemy
    - [2] `Debt` cost=0 canPlay=False target=None
    - [3] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [4] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `STABBOT` 23/23 block=0 → Attack 11×1 + Debuff  powers=[MINION_POWER:1]
    - [1] `FABRICATOR` 144/150 block=0 → Attack 18×1 + Unknown

  → play card [0]
  → play card [0] target=0
  → end_turn → round transition
#### Round 3  (e=3/3 block=0 draw=6 disc=0)

  - hand:
    - [0] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [1] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [2] `Debt` cost=0 canPlay=False target=None
    - [3] `DefendIronclad` cost=1 canPlay=True target=Self
    - [4] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `STABBOT` 15/23 block=0 → Attack 11×1 + Debuff  powers=[MINION_POWER:1,VULNERABLE_POWER:1]
    - [1] `ZAPBOT` 20/20 block=0 → Attack 14×1  powers=[HIGH_VOLTAGE_POWER:2,MINION_POWER:1,STRENGTH_POWER:2]
    - [2] `FABRICATOR` 144/150 block=0 → Attack 18×1 + Unknown
  - player powers: FRAIL_POWER:1

  → play card [0] target=0
  → play card [0] target=0
  → play card [1]
  → end_turn → round transition
#### Round 4  (e=3/3 block=0 draw=1 disc=5)

  - hand:
    - [0] `DefendIronclad` cost=1 canPlay=True target=Self
    - [1] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [2] `Bash` cost=2 canPlay=True target=AnyEnemy
    - [3] `DefendIronclad` cost=1 canPlay=True target=Self
    - [4] `DefendIronclad` cost=1 canPlay=True target=Self
  - enemies:
    - [0] `STABBOT` 23/23 block=0 → Attack 11×1 + Debuff  powers=[MINION_POWER:1]
    - [1] `ZAPBOT` 20/20 block=0 → Attack 14×1  powers=[HIGH_VOLTAGE_POWER:2,MINION_POWER:1,STRENGTH_POWER:4]
    - [2] `FABRICATOR` 144/150 block=0 → Unknown
  - player powers: FRAIL_POWER:1

  → play card [0]
  → play card [0] target=0
  → play card [1]
  → end_turn → round transition
  - rewards offered:
    - [0] gold 15g  canSkip=False
    - [1] potion potion=`POTION.FIRE_POTION (61407752)`  canSkip=False
    - [2] card cards=[`Taunt`(cost=1), `PerfectedStrike`(cost=2), `Thunderclap`(cost=1)]  canSkip=True

  - rewards offered:
    - [0] gold 15g  canSkip=False
    - [1] potion potion=`POTION.FIRE_POTION (61407752)`  canSkip=False
    - [2] card cards=[`Taunt`(cost=1), `PerfectedStrike`(cost=2), `Thunderclap`(cost=1)]  canSkip=True

  - heal → hp=80/80

  - rewards offered:
    - [0] gold 15g  canSkip=False
    - [1] potion potion=`POTION.FIRE_POTION (61407752)`  canSkip=False
    - [2] card cards=[`Taunt`(cost=1), `PerfectedStrike`(cost=2), `Thunderclap`(cost=1)]  canSkip=True

  → claim reward [0] → hp=80 room=CombatRoom
  - rewards offered:
    - [0] potion potion=`POTION.FIRE_POTION (61407752)`  canSkip=False
    - [1] card cards=[`Taunt`(cost=1), `PerfectedStrike`(cost=2), `Thunderclap`(cost=1)]  canSkip=True

  - rewards offered:
    - [0] potion potion=`POTION.FIRE_POTION (61407752)`  canSkip=False
    - [1] card cards=[`Taunt`(cost=1), `PerfectedStrike`(cost=2), `Thunderclap`(cost=1)]  canSkip=True

  → claim reward [0] → hp=80 room=CombatRoom
  - rewards offered:
    - [0] card cards=[`Taunt`(cost=1), `PerfectedStrike`(cost=2), `Thunderclap`(cost=1)]  canSkip=True

  - rewards offered:
    - [0] card cards=[`Taunt`(cost=1), `PerfectedStrike`(cost=2), `Thunderclap`(cost=1)]  canSkip=True

## Floor 12: MapRoom  (hp=80)

  - map options: (4,12):Unknown

  → skip reward [0] → hp=80 room=MapRoom
  - combat ended (hp=80)

## Floor 13: MerchantRoom  (hp=80)


  → pick map (4,12) → MerchantRoom floor=13
## Floor 13: MapRoom  (hp=80)

  - map options: (4,13):RestSite

  → leave merchant → MapRoom
## Floor 14: RestSiteRoom  (hp=80)


  → pick map (4,13) → RestSiteRoom floor=14
## Floor 14: MapRoom  (hp=80)

  - map options: (3,14):Boss

  → pick rest option [0] → MapRoom hp=80
## Floor 15: BossRoom  (hp=80)


  → pick map (3,14) → BossRoom floor=15
### Combat #1 on floor 15

#### Round 1  (e=3/3 block=0 draw=6 disc=0)

  - hand:
    - [0] `DefendIronclad` cost=1 canPlay=True target=Self
    - [1] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [2] `Debt` cost=0 canPlay=False target=None
    - [3] `DefendIronclad` cost=1 canPlay=True target=Self
    - [4] `DefendIronclad` cost=1 canPlay=True target=Self
  - enemies:
    - [0] `TEST_SUBJECT` 100/100 block=0 → Attack 20×1  powers=[ADAPTABLE_POWER:1,ENRAGE_POWER:2]

  → play card [0]
  → play card [0] target=0
  → play card [1]
  → end_turn → round transition
#### Round 2  (e=3/3 block=0 draw=1 disc=5)

  - hand:
    - [0] `DefendIronclad` cost=1 canPlay=True target=Self
    - [1] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [2] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [3] `Bash` cost=2 canPlay=True target=AnyEnemy
    - [4] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `TEST_SUBJECT` 94/100 block=0 → Attack 14×1 + Debuff  powers=[ADAPTABLE_POWER:1,ENRAGE_POWER:2,STRENGTH_POWER:4]

  → play card [0]
  → play card [0] target=0
  → play card [0] target=0
  → end_turn → round transition
#### Round 3  (e=3/3 block=0 draw=6 disc=0)

  - hand:
    - [0] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [1] `DefendIronclad` cost=1 canPlay=True target=Self
    - [2] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [3] `Bash` cost=2 canPlay=True target=AnyEnemy
    - [4] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `TEST_SUBJECT` 82/100 block=0 → Attack 20×1  powers=[ADAPTABLE_POWER:1,ENRAGE_POWER:2,STRENGTH_POWER:6]
  - player powers: VULNERABLE_POWER:1

  → play card [0] target=0
  → play card [0]
  → play card [0] target=0
  → end_turn → round transition
#### Round 4  (e=3/3 block=0 draw=1 disc=5)

  - hand:
    - [0] `DefendIronclad` cost=1 canPlay=True target=Self
    - [1] `Debt` cost=0 canPlay=False target=None
    - [2] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [3] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [4] `DefendIronclad` cost=1 canPlay=True target=Self
  - enemies:
    - [0] `TEST_SUBJECT` 70/100 block=0 → Attack 14×1 + Debuff  powers=[ADAPTABLE_POWER:1,ENRAGE_POWER:2,STRENGTH_POWER:8]
  - player powers: VULNERABLE_POWER:1

  → play card [0]
  → play card [1] target=0
  → play card [1] target=0
  → end_turn → round transition
#### Round 5  (e=3/3 block=0 draw=6 disc=0)

  - hand:
    - [0] `DefendIronclad` cost=1 canPlay=True target=Self
    - [1] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [2] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [3] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [4] `DefendIronclad` cost=1 canPlay=True target=Self
  - enemies:
    - [0] `TEST_SUBJECT` 58/100 block=0 → Attack 20×1  powers=[ADAPTABLE_POWER:1,ENRAGE_POWER:2,STRENGTH_POWER:10]
  - player powers: VULNERABLE_POWER:2

  → play card [0]
  → play card [0] target=0
  → play card [0] target=0
  → end_turn → round transition
  - heal → hp=80/80

  → end_turn → round transition
  → end_turn → round transition
  → end_turn → round transition
  → end_turn → round transition
  → end_turn → round transition
  → end_turn → round transition
  → end_turn → round transition
  → end_turn → round transition
  → end_turn → round transition
