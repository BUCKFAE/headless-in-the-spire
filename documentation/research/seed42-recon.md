# Seed 42 Recon — Ironclad

- character: `Ironclad`
- seed: `42`
- starting relics: `BURNING_BLOOD`

## Floor 0: MapRoom  (hp=80/80)

  - map options: (3,0):Monster, (0,1):Monster, (3,1):Monster, (5,1):Monster

## Floor 2: CombatRoom  (hp=80)


  → pick map (0,1) → CombatRoom floor=2
### Combat #1 on floor 2

#### Round 1  (e=3/3 block=0 draw=5 disc=0)

  - hand:
    - [0] `BASH` cost=2 canPlay=True target=AnyEnemy
    - [1] `STRIKE_IRONCLAD` cost=1 canPlay=True target=AnyEnemy
    - [2] `STRIKE_IRONCLAD` cost=1 canPlay=True target=AnyEnemy
    - [3] `DEFEND_IRONCLAD` cost=1 canPlay=True target=Self
    - [4] `DEFEND_IRONCLAD` cost=1 canPlay=True target=Self
  - enemies:
    - [0] `FUZZY_WURM_CRAWLER` 55/55 block=0 → Attack 4×1

  → play card [0] target=0
  → play card [0] target=0
  → end_turn → round transition
#### Round 2  (e=3/3 block=0 draw=0 disc=5)

  - hand:
    - [0] `STRIKE_IRONCLAD` cost=1 canPlay=True target=AnyEnemy
    - [1] `STRIKE_IRONCLAD` cost=1 canPlay=True target=AnyEnemy
    - [2] `DEFEND_IRONCLAD` cost=1 canPlay=True target=Self
    - [3] `DEFEND_IRONCLAD` cost=1 canPlay=True target=Self
    - [4] `STRIKE_IRONCLAD` cost=1 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `FUZZY_WURM_CRAWLER` 38/55 block=0 → Buff  powers=[VULNERABLE_POWER:1]

  → play card [0] target=0
  → play card [0] target=0
  → play card [0]
  → end_turn → round transition
#### Round 3  (e=3/3 block=0 draw=5 disc=0)

  - hand:
    - [0] `STRIKE_IRONCLAD` cost=1 canPlay=True target=AnyEnemy
    - [1] `STRIKE_IRONCLAD` cost=1 canPlay=True target=AnyEnemy
    - [2] `STRIKE_IRONCLAD` cost=1 canPlay=True target=AnyEnemy
    - [3] `STRIKE_IRONCLAD` cost=1 canPlay=True target=AnyEnemy
    - [4] `DEFEND_IRONCLAD` cost=1 canPlay=True target=Self
  - enemies:
    - [0] `FUZZY_WURM_CRAWLER` 20/55 block=0 → Attack 4×1  powers=[STRENGTH_POWER:7]

  → play card [0] target=0
  → play card [0] target=0
  → play card [0] target=0
  → end_turn → round transition
#### Round 4  (e=3/3 block=0 draw=0 disc=5)

  - hand:
    - [0] `DEFEND_IRONCLAD` cost=1 canPlay=True target=Self
    - [1] `DEFEND_IRONCLAD` cost=1 canPlay=True target=Self
    - [2] `DEFEND_IRONCLAD` cost=1 canPlay=True target=Self
    - [3] `BASH` cost=2 canPlay=True target=AnyEnemy
    - [4] `STRIKE_IRONCLAD` cost=1 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `FUZZY_WURM_CRAWLER` 2/55 block=0 → Attack 4×1  powers=[STRENGTH_POWER:7]

  → play card [0]
  → play card [0]
  → play card [0]
  → end_turn → round transition
#### Round 5  (e=3/3 block=0 draw=5 disc=0)

  - hand:
    - [0] `DEFEND_IRONCLAD` cost=1 canPlay=True target=Self
    - [1] `STRIKE_IRONCLAD` cost=1 canPlay=True target=AnyEnemy
    - [2] `STRIKE_IRONCLAD` cost=1 canPlay=True target=AnyEnemy
    - [3] `DEFEND_IRONCLAD` cost=1 canPlay=True target=Self
    - [4] `DEFEND_IRONCLAD` cost=1 canPlay=True target=Self
  - enemies:
    - [0] `FUZZY_WURM_CRAWLER` 2/55 block=0 → Buff  powers=[STRENGTH_POWER:7]

  → play card [0]
  → play card [0] target=0
  - rewards offered:
    - [0] gold 12g  canSkip=False
    - [1] potion potion=`POTION.ENERGY_POTION (46888886)`  canSkip=False
    - [2] card cards=[`BODY_SLAM`(cost=1), `TREMBLE`(cost=1), `SWORD_BOOMERANG`(cost=1)]  canSkip=True

  → claim reward [0] → hp=71 room=CombatRoom
  - rewards offered:
    - [0] potion potion=`POTION.ENERGY_POTION (46888886)`  canSkip=False
    - [1] card cards=[`BODY_SLAM`(cost=1), `TREMBLE`(cost=1), `SWORD_BOOMERANG`(cost=1)]  canSkip=True

  → claim reward [0] → hp=71 room=CombatRoom
  - rewards offered:
    - [0] card cards=[`BODY_SLAM`(cost=1), `TREMBLE`(cost=1), `SWORD_BOOMERANG`(cost=1)]  canSkip=True

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
    - [0] `STRIKE_IRONCLAD` cost=1 canPlay=True target=AnyEnemy
    - [1] `DEFEND_IRONCLAD` cost=1 canPlay=True target=Self
    - [2] `DEFEND_IRONCLAD` cost=1 canPlay=True target=Self
    - [3] `STRIKE_IRONCLAD` cost=1 canPlay=True target=AnyEnemy
    - [4] `STRIKE_IRONCLAD` cost=1 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `SHRINKER_BEETLE` 39/39 block=0 → Unknown

  → play card [0] target=0
  → play card [0]
  → play card [0]
  → end_turn → round transition
#### Round 2  (e=3/3 block=0 draw=0 disc=5)

  - hand:
    - [0] `STRIKE_IRONCLAD` cost=1 canPlay=True target=AnyEnemy
    - [1] `DEFEND_IRONCLAD` cost=1 canPlay=True target=Self
    - [2] `BASH` cost=2 canPlay=True target=AnyEnemy
    - [3] `DEFEND_IRONCLAD` cost=1 canPlay=True target=Self
    - [4] `STRIKE_IRONCLAD` cost=1 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `SHRINKER_BEETLE` 33/39 block=0 → Attack 7×1
  - player powers: SHRINK_POWER:-1

  → play card [0] target=0
  → play card [0]
  → play card [1]
  → end_turn → round transition
#### Round 3  (e=3/3 block=0 draw=5 disc=0)

  - hand:
    - [0] `STRIKE_IRONCLAD` cost=1 canPlay=True target=AnyEnemy
    - [1] `DEFEND_IRONCLAD` cost=1 canPlay=True target=Self
    - [2] `DEFEND_IRONCLAD` cost=1 canPlay=True target=Self
    - [3] `DEFEND_IRONCLAD` cost=1 canPlay=True target=Self
    - [4] `STRIKE_IRONCLAD` cost=1 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `SHRINKER_BEETLE` 29/39 block=0 → Attack 13×1
  - player powers: SHRINK_POWER:-1

  → play card [0] target=0
  → play card [0]
  → play card [0]
  → end_turn → round transition
#### Round 4  (e=3/3 block=0 draw=0 disc=5)

  - hand:
    - [0] `STRIKE_IRONCLAD` cost=1 canPlay=True target=AnyEnemy
    - [1] `DEFEND_IRONCLAD` cost=1 canPlay=True target=Self
    - [2] `STRIKE_IRONCLAD` cost=1 canPlay=True target=AnyEnemy
    - [3] `STRIKE_IRONCLAD` cost=1 canPlay=True target=AnyEnemy
    - [4] `BASH` cost=2 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `SHRINKER_BEETLE` 25/39 block=0 → Attack 7×1
  - player powers: SHRINK_POWER:-1

  → play card [0] target=0
  → play card [0]
  → play card [0] target=0
  → end_turn → round transition
#### Round 5  (e=3/3 block=0 draw=5 disc=0)

  - hand:
    - [0] `DEFEND_IRONCLAD` cost=1 canPlay=True target=Self
    - [1] `DEFEND_IRONCLAD` cost=1 canPlay=True target=Self
    - [2] `DEFEND_IRONCLAD` cost=1 canPlay=True target=Self
    - [3] `STRIKE_IRONCLAD` cost=1 canPlay=True target=AnyEnemy
    - [4] `STRIKE_IRONCLAD` cost=1 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `SHRINKER_BEETLE` 17/39 block=0 → Attack 13×1
  - player powers: SHRINK_POWER:-1

  → play card [0]
  → play card [0]
  → play card [0]
  → end_turn → round transition
#### Round 6  (e=3/3 block=0 draw=0 disc=5)

  - hand:
    - [0] `STRIKE_IRONCLAD` cost=1 canPlay=True target=AnyEnemy
    - [1] `STRIKE_IRONCLAD` cost=1 canPlay=True target=AnyEnemy
    - [2] `DEFEND_IRONCLAD` cost=1 canPlay=True target=Self
    - [3] `STRIKE_IRONCLAD` cost=1 canPlay=True target=AnyEnemy
    - [4] `BASH` cost=2 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `SHRINKER_BEETLE` 17/39 block=0 → Attack 7×1
  - player powers: SHRINK_POWER:-1

  → play card [0] target=0
  → play card [0] target=0
  → play card [0]
  → end_turn → round transition
#### Round 7  (e=3/3 block=0 draw=5 disc=0)

  - hand:
    - [0] `DEFEND_IRONCLAD` cost=1 canPlay=True target=Self
    - [1] `STRIKE_IRONCLAD` cost=1 canPlay=True target=AnyEnemy
    - [2] `STRIKE_IRONCLAD` cost=1 canPlay=True target=AnyEnemy
    - [3] `BASH` cost=2 canPlay=True target=AnyEnemy
    - [4] `DEFEND_IRONCLAD` cost=1 canPlay=True target=Self
  - enemies:
    - [0] `SHRINKER_BEETLE` 9/39 block=0 → Attack 13×1
  - player powers: SHRINK_POWER:-1

  → play card [0]
  → play card [0] target=0
  → play card [0] target=0
  → end_turn → round transition
#### Round 8  (e=3/3 block=0 draw=0 disc=5)

  - hand:
    - [0] `STRIKE_IRONCLAD` cost=1 canPlay=True target=AnyEnemy
    - [1] `DEFEND_IRONCLAD` cost=1 canPlay=True target=Self
    - [2] `STRIKE_IRONCLAD` cost=1 canPlay=True target=AnyEnemy
    - [3] `STRIKE_IRONCLAD` cost=1 canPlay=True target=AnyEnemy
    - [4] `DEFEND_IRONCLAD` cost=1 canPlay=True target=Self
  - enemies:
    - [0] `SHRINKER_BEETLE` 1/39 block=0 → Attack 7×1
  - player powers: SHRINK_POWER:-1

  → play card [0] target=0
  - rewards offered:
    - [0] gold 10g  canSkip=False
    - [1] card cards=[`HEADBUTT`(cost=1), `EXPECT_A_FIGHT`(cost=2), `BURNING_PACT`(cost=1)]  canSkip=True

  → claim reward [0] → hp=71 room=CombatRoom
  - rewards offered:
    - [0] card cards=[`HEADBUTT`(cost=1), `EXPECT_A_FIGHT`(cost=2), `BURNING_PACT`(cost=1)]  canSkip=True

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
    - [0] `STRIKE_IRONCLAD` cost=1 canPlay=True target=AnyEnemy
    - [1] `DEFEND_IRONCLAD` cost=1 canPlay=True target=Self
    - [2] `STRIKE_IRONCLAD` cost=1 canPlay=True target=AnyEnemy
    - [3] `STRIKE_IRONCLAD` cost=1 canPlay=True target=AnyEnemy
    - [4] `DEFEND_IRONCLAD` cost=1 canPlay=True target=Self
  - enemies:
    - [0] `NIBBIT` 44/44 block=0 → Attack 12×1

  → play card [0] target=0
  → play card [0]
  → play card [0] target=0
  → end_turn → round transition
#### Round 2  (e=3/3 block=0 draw=0 disc=5)

  - hand:
    - [0] `STRIKE_IRONCLAD` cost=1 canPlay=True target=AnyEnemy
    - [1] `STRIKE_IRONCLAD` cost=1 canPlay=True target=AnyEnemy
    - [2] `DEFEND_IRONCLAD` cost=1 canPlay=True target=Self
    - [3] `BASH` cost=2 canPlay=True target=AnyEnemy
    - [4] `DEFEND_IRONCLAD` cost=1 canPlay=True target=Self
  - enemies:
    - [0] `NIBBIT` 32/44 block=0 → Attack 6×1 + Defend

  → play card [0] target=0
  → play card [0] target=0
  → play card [0]
  → end_turn → round transition
#### Round 3  (e=3/3 block=0 draw=5 disc=0)

  - hand:
    - [0] `DEFEND_IRONCLAD` cost=1 canPlay=True target=Self
    - [1] `DEFEND_IRONCLAD` cost=1 canPlay=True target=Self
    - [2] `STRIKE_IRONCLAD` cost=1 canPlay=True target=AnyEnemy
    - [3] `DEFEND_IRONCLAD` cost=1 canPlay=True target=Self
    - [4] `DEFEND_IRONCLAD` cost=1 canPlay=True target=Self
  - enemies:
    - [0] `NIBBIT` 20/44 block=5 → Buff

  → play card [0]
  → play card [0]
  → play card [0] target=0
  → end_turn → round transition
#### Round 4  (e=3/3 block=0 draw=0 disc=5)

  - hand:
    - [0] `STRIKE_IRONCLAD` cost=1 canPlay=True target=AnyEnemy
    - [1] `BASH` cost=2 canPlay=True target=AnyEnemy
    - [2] `STRIKE_IRONCLAD` cost=1 canPlay=True target=AnyEnemy
    - [3] `STRIKE_IRONCLAD` cost=1 canPlay=True target=AnyEnemy
    - [4] `STRIKE_IRONCLAD` cost=1 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `NIBBIT` 19/44 block=0 → Attack 12×1  powers=[STRENGTH_POWER:2]

  → play card [0] target=0
  → play card [0] target=0
  → end_turn → round transition
#### Round 5  (e=3/3 block=0 draw=5 disc=0)

  - hand:
    - [0] `DEFEND_IRONCLAD` cost=1 canPlay=True target=Self
    - [1] `STRIKE_IRONCLAD` cost=1 canPlay=True target=AnyEnemy
    - [2] `STRIKE_IRONCLAD` cost=1 canPlay=True target=AnyEnemy
    - [3] `STRIKE_IRONCLAD` cost=1 canPlay=True target=AnyEnemy
    - [4] `DEFEND_IRONCLAD` cost=1 canPlay=True target=Self
  - enemies:
    - [0] `NIBBIT` 5/44 block=0 → Attack 6×1 + Defend  powers=[STRENGTH_POWER:2,VULNERABLE_POWER:1]

  → play card [0]
  → play card [0] target=0
  - rewards offered:
    - [0] gold 10g  canSkip=False
    - [1] card cards=[`BULLY`(cost=0), `THUNDERCLAP`(cost=1), `BLUDGEON`(cost=3)]  canSkip=True

  → claim reward [0] → hp=64 room=CombatRoom
  - rewards offered:
    - [0] card cards=[`BULLY`(cost=0), `THUNDERCLAP`(cost=1), `BLUDGEON`(cost=3)]  canSkip=True

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
    - [0] `DEFEND_IRONCLAD` cost=1 canPlay=True target=Self
    - [1] `STRIKE_IRONCLAD` cost=1 canPlay=True target=AnyEnemy
    - [2] `DEFEND_IRONCLAD` cost=1 canPlay=True target=Self
    - [3] `STRIKE_IRONCLAD` cost=1 canPlay=True target=AnyEnemy
    - [4] `DEFEND_IRONCLAD` cost=1 canPlay=True target=Self
  - enemies:
    - [0] `PHROG_PARASITE` 64/64 block=0 → Unknown  powers=[INFESTED_POWER:4]

  → play card [0]
  → play card [0] target=0
  → play card [0]
  → end_turn → round transition
#### Round 2  (e=3/3 block=0 draw=0 disc=8)

  - hand:
    - [0] `STRIKE_IRONCLAD` cost=1 canPlay=True target=AnyEnemy
    - [1] `BASH` cost=2 canPlay=True target=AnyEnemy
    - [2] `STRIKE_IRONCLAD` cost=1 canPlay=True target=AnyEnemy
    - [3] `DEFEND_IRONCLAD` cost=1 canPlay=True target=Self
    - [4] `STRIKE_IRONCLAD` cost=1 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `PHROG_PARASITE` 58/64 block=0 → Attack 4×4  powers=[INFESTED_POWER:4]

  → play card [0] target=0
  → play card [0] target=0
  → end_turn → round transition
#### Round 3  (e=3/3 block=0 draw=8 disc=0)

  - hand:
    - [0] `DEFEND_IRONCLAD` cost=1 canPlay=True target=Self
    - [1] `STRIKE_IRONCLAD` cost=1 canPlay=True target=AnyEnemy
    - [2] `INFECTION` cost=0 canPlay=False target=None
    - [3] `STRIKE_IRONCLAD` cost=1 canPlay=True target=AnyEnemy
    - [4] `INFECTION` cost=0 canPlay=False target=None
  - enemies:
    - [0] `PHROG_PARASITE` 44/64 block=0 → Unknown  powers=[INFESTED_POWER:4,VULNERABLE_POWER:1]

  → play card [0]
  → play card [0] target=0
  → play card [1] target=0
  → end_turn → round transition
#### Round 4  (e=3/3 block=0 draw=3 disc=8)

  - hand:
    - [0] `STRIKE_IRONCLAD` cost=1 canPlay=True target=AnyEnemy
    - [1] `STRIKE_IRONCLAD` cost=1 canPlay=True target=AnyEnemy
    - [2] `DEFEND_IRONCLAD` cost=1 canPlay=True target=Self
    - [3] `DEFEND_IRONCLAD` cost=1 canPlay=True target=Self
    - [4] `STRIKE_IRONCLAD` cost=1 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `PHROG_PARASITE` 26/64 block=0 → Attack 4×4  powers=[INFESTED_POWER:4]

  → play card [0] target=0
  → play card [0] target=0
  → play card [0]
  → end_turn → round transition
#### Round 5  (e=3/3 block=0 draw=11 disc=0)

  - hand:
    - [0] `DEFEND_IRONCLAD` cost=1 canPlay=True target=Self
    - [1] `INFECTION` cost=0 canPlay=False target=None
    - [2] `BASH` cost=2 canPlay=True target=AnyEnemy
    - [3] `INFECTION` cost=0 canPlay=False target=None
    - [4] `INFECTION` cost=0 canPlay=False target=None
  - enemies:
    - [0] `PHROG_PARASITE` 14/64 block=0 → Unknown  powers=[INFESTED_POWER:4]

  → play card [0]
  → play card [1] target=0
  → end_turn → round transition
#### Round 6  (e=3/3 block=0 draw=6 disc=8)

  - hand:
    - [0] `INFECTION` cost=0 canPlay=False target=None
    - [1] `STRIKE_IRONCLAD` cost=1 canPlay=True target=AnyEnemy
    - [2] `DEFEND_IRONCLAD` cost=1 canPlay=True target=Self
    - [3] `INFECTION` cost=0 canPlay=False target=None
    - [4] `STRIKE_IRONCLAD` cost=1 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `PHROG_PARASITE` 6/64 block=0 → Attack 4×4  powers=[INFESTED_POWER:4,VULNERABLE_POWER:1]

  → play card [1] target=0
  → play card [1]
  → play card [2] target=0
  → end_turn → round transition
#### Round 7  (e=3/3 block=0 draw=1 disc=13)

  - hand:
    - [0] `STRIKE_IRONCLAD` cost=1 canPlay=True target=AnyEnemy
    - [1] `INFECTION` cost=0 canPlay=False target=None
    - [2] `DEFEND_IRONCLAD` cost=1 canPlay=True target=Self
    - [3] `STRIKE_IRONCLAD` cost=1 canPlay=True target=AnyEnemy
    - [4] `STRIKE_IRONCLAD` cost=1 canPlay=True target=AnyEnemy
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
    - [0] `DEFEND_IRONCLAD` cost=1 canPlay=True target=Self
    - [1] `INFECTION` cost=0 canPlay=False target=None
    - [2] `INFECTION` cost=0 canPlay=False target=None
    - [3] `INFECTION` cost=0 canPlay=False target=None
    - [4] `DEFEND_IRONCLAD` cost=1 canPlay=True target=Self
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
    - [0] `INFECTION` cost=0 canPlay=False target=None
    - [1] `INFECTION` cost=0 canPlay=False target=None
    - [2] `DEFEND_IRONCLAD` cost=1 canPlay=True target=Self
    - [3] `INFECTION` cost=0 canPlay=False target=None
    - [4] `INFECTION` cost=0 canPlay=False target=None
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
    - [2] card cards=[`DISMANTLE`(cost=1), `CASCADE`(cost=0), `THUNDERCLAP`(cost=1)]  canSkip=True

  → claim reward [0] → hp=0 room=CombatRoom
  - rewards offered:
    - [0] relic relic=``  canSkip=False
    - [1] card cards=[`DISMANTLE`(cost=1), `CASCADE`(cost=0), `THUNDERCLAP`(cost=1)]  canSkip=True

  → claim reward [0] → hp=0 room=CombatRoom
  - rewards offered:
    - [0] card cards=[`DISMANTLE`(cost=1), `CASCADE`(cost=0), `THUNDERCLAP`(cost=1)]  canSkip=True

## Floor 8: MapRoom  (hp=0)

  - map options: (0,8):Monster

  → skip reward [0] → hp=0 room=MapRoom
  - combat ended (hp=0)

  - heal → hp=80/80

## Floor 9: CombatRoom  (hp=80)


  → pick map (0,8) → CombatRoom floor=9
### Combat #1 on floor 9

#### Round 1  (e=3/3 block=0 draw=5 disc=0)

  - hand:
    - [0] `STRIKE_IRONCLAD` cost=1 canPlay=True target=AnyEnemy
    - [1] `DEFEND_IRONCLAD` cost=1 canPlay=True target=Self
    - [2] `DEFEND_IRONCLAD` cost=1 canPlay=True target=Self
    - [3] `STRIKE_IRONCLAD` cost=1 canPlay=True target=AnyEnemy
    - [4] `DEFEND_IRONCLAD` cost=1 canPlay=True target=Self
  - enemies:
    - [0] `MAWLER` 72/72 block=0 → Attack 4×2

  → play card [0] target=0
  → play card [0]
  → play card [0]
  → end_turn → round transition
#### Round 2  (e=3/3 block=0 draw=0 disc=5)

  - hand:
    - [0] `BASH` cost=2 canPlay=True target=AnyEnemy
    - [1] `STRIKE_IRONCLAD` cost=1 canPlay=True target=AnyEnemy
    - [2] `STRIKE_IRONCLAD` cost=1 canPlay=True target=AnyEnemy
    - [3] `STRIKE_IRONCLAD` cost=1 canPlay=True target=AnyEnemy
    - [4] `DEFEND_IRONCLAD` cost=1 canPlay=True target=Self
  - enemies:
    - [0] `MAWLER` 66/72 block=0 → Attack 14×1

  → play card [0] target=0
  → play card [0] target=0
  → end_turn → round transition
#### Round 3  (e=3/3 block=0 draw=5 disc=0)

  - hand:
    - [0] `STRIKE_IRONCLAD` cost=1 canPlay=True target=AnyEnemy
    - [1] `DEFEND_IRONCLAD` cost=1 canPlay=True target=Self
    - [2] `STRIKE_IRONCLAD` cost=1 canPlay=True target=AnyEnemy
    - [3] `STRIKE_IRONCLAD` cost=1 canPlay=True target=AnyEnemy
    - [4] `DEFEND_IRONCLAD` cost=1 canPlay=True target=Self
  - enemies:
    - [0] `MAWLER` 49/72 block=0 → Debuff  powers=[VULNERABLE_POWER:1]

  → play card [0] target=0
  → play card [0]
  → play card [0] target=0
  → end_turn → round transition
#### Round 4  (e=3/3 block=0 draw=0 disc=5)

  - hand:
    - [0] `DEFEND_IRONCLAD` cost=1 canPlay=True target=Self
    - [1] `BASH` cost=2 canPlay=True target=AnyEnemy
    - [2] `DEFEND_IRONCLAD` cost=1 canPlay=True target=Self
    - [3] `STRIKE_IRONCLAD` cost=1 canPlay=True target=AnyEnemy
    - [4] `STRIKE_IRONCLAD` cost=1 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `MAWLER` 31/72 block=0 → Attack 4×2
  - player powers: VULNERABLE_POWER:3

  → play card [0]
  → play card [0] target=0
  → end_turn → round transition
#### Round 5  (e=3/3 block=0 draw=5 disc=0)

  - hand:
    - [0] `DEFEND_IRONCLAD` cost=1 canPlay=True target=Self
    - [1] `STRIKE_IRONCLAD` cost=1 canPlay=True target=AnyEnemy
    - [2] `STRIKE_IRONCLAD` cost=1 canPlay=True target=AnyEnemy
    - [3] `STRIKE_IRONCLAD` cost=1 canPlay=True target=AnyEnemy
    - [4] `STRIKE_IRONCLAD` cost=1 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `MAWLER` 23/72 block=0 → Attack 14×1  powers=[VULNERABLE_POWER:1]
  - player powers: VULNERABLE_POWER:3

  → play card [0]
  → play card [0] target=0
  → play card [0] target=0
  → end_turn → round transition
#### Round 6  (e=3/3 block=0 draw=0 disc=5)

  - hand:
    - [0] `DEFEND_IRONCLAD` cost=1 canPlay=True target=Self
    - [1] `BASH` cost=2 canPlay=True target=AnyEnemy
    - [2] `STRIKE_IRONCLAD` cost=1 canPlay=True target=AnyEnemy
    - [3] `DEFEND_IRONCLAD` cost=1 canPlay=True target=Self
    - [4] `DEFEND_IRONCLAD` cost=1 canPlay=True target=Self
  - enemies:
    - [0] `MAWLER` 5/72 block=0 → Attack 4×2
  - player powers: VULNERABLE_POWER:3

  → play card [0]
  → play card [0] target=0
  - rewards offered:
    - [0] gold 18g  canSkip=False
    - [1] potion potion=`POTION.ENTROPIC_BREW (52645056)`  canSkip=False
    - [2] card cards=[`UPPERCUT`(cost=2), `ARMAMENTS`(cost=1), `STONE_ARMOR`(cost=1)]  canSkip=True

  → claim reward [0] → hp=54 room=CombatRoom
  - rewards offered:
    - [0] potion potion=`POTION.ENTROPIC_BREW (52645056)`  canSkip=False
    - [1] card cards=[`UPPERCUT`(cost=2), `ARMAMENTS`(cost=1), `STONE_ARMOR`(cost=1)]  canSkip=True

  → claim reward [0] → hp=54 room=CombatRoom
  - rewards offered:
    - [0] card cards=[`UPPERCUT`(cost=2), `ARMAMENTS`(cost=1), `STONE_ARMOR`(cost=1)]  canSkip=True

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
    - [0] `DEFEND_IRONCLAD` cost=1 canPlay=True target=Self
    - [1] `STRIKE_IRONCLAD` cost=1 canPlay=True target=AnyEnemy
    - [2] `STRIKE_IRONCLAD` cost=1 canPlay=True target=AnyEnemy
    - [3] `DEFEND_IRONCLAD` cost=1 canPlay=True target=Self
    - [4] `STRIKE_IRONCLAD` cost=1 canPlay=True target=AnyEnemy
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
    - [0] `STRIKE_IRONCLAD` cost=1 canPlay=True target=AnyEnemy
    - [1] `BASH` cost=2 canPlay=True target=AnyEnemy
    - [2] `STRIKE_IRONCLAD` cost=1 canPlay=True target=AnyEnemy
    - [3] `DEFEND_IRONCLAD` cost=1 canPlay=True target=Self
    - [4] `DEFEND_IRONCLAD` cost=1 canPlay=True target=Self
  - enemies:
    - [0] `TWIG_SLIME_S` 9/9 block=0 → Attack 4×1
    - [1] `SLITHERING_STRANGLER` 55/55 block=0 → Attack 7×1 + Defend
  - player powers: CONSTRICT_POWER:3

  → play card [0] target=0
  → play card [0] target=0
  → end_turn → round transition
#### Round 3  (e=3/3 block=0 draw=5 disc=0)

  - hand:
    - [0] `STRIKE_IRONCLAD` cost=1 canPlay=True target=AnyEnemy
    - [1] `STRIKE_IRONCLAD` cost=1 canPlay=True target=AnyEnemy
    - [2] `DEFEND_IRONCLAD` cost=1 canPlay=True target=Self
    - [3] `STRIKE_IRONCLAD` cost=1 canPlay=True target=AnyEnemy
    - [4] `STRIKE_IRONCLAD` cost=1 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `SLITHERING_STRANGLER` 55/55 block=5 → Debuff
  - player powers: CONSTRICT_POWER:3

  → play card [0] target=0
  → play card [0] target=0
  → play card [0]
  → end_turn → round transition
#### Round 4  (e=3/3 block=0 draw=0 disc=5)

  - hand:
    - [0] `DEFEND_IRONCLAD` cost=1 canPlay=True target=Self
    - [1] `BASH` cost=2 canPlay=True target=AnyEnemy
    - [2] `STRIKE_IRONCLAD` cost=1 canPlay=True target=AnyEnemy
    - [3] `DEFEND_IRONCLAD` cost=1 canPlay=True target=Self
    - [4] `DEFEND_IRONCLAD` cost=1 canPlay=True target=Self
  - enemies:
    - [0] `SLITHERING_STRANGLER` 48/55 block=0 → Attack 7×1 + Defend
  - player powers: CONSTRICT_POWER:6

  → play card [0]
  → play card [0] target=0
  → end_turn → round transition
#### Round 5  (e=3/3 block=0 draw=5 disc=0)

  - hand:
    - [0] `DEFEND_IRONCLAD` cost=1 canPlay=True target=Self
    - [1] `STRIKE_IRONCLAD` cost=1 canPlay=True target=AnyEnemy
    - [2] `DEFEND_IRONCLAD` cost=1 canPlay=True target=Self
    - [3] `STRIKE_IRONCLAD` cost=1 canPlay=True target=AnyEnemy
    - [4] `STRIKE_IRONCLAD` cost=1 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `SLITHERING_STRANGLER` 40/55 block=5 → Debuff  powers=[VULNERABLE_POWER:1]
  - player powers: CONSTRICT_POWER:6

  → play card [0]
  → play card [0] target=0
  → play card [0]
  → end_turn → round transition
#### Round 6  (e=3/3 block=0 draw=0 disc=5)

  - hand:
    - [0] `STRIKE_IRONCLAD` cost=1 canPlay=True target=AnyEnemy
    - [1] `DEFEND_IRONCLAD` cost=1 canPlay=True target=Self
    - [2] `BASH` cost=2 canPlay=True target=AnyEnemy
    - [3] `DEFEND_IRONCLAD` cost=1 canPlay=True target=Self
    - [4] `STRIKE_IRONCLAD` cost=1 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `SLITHERING_STRANGLER` 36/55 block=0 → Attack 7×1 + Defend
  - player powers: CONSTRICT_POWER:9

  → play card [0] target=0
  → play card [0]
  → play card [1]
  → end_turn → round transition
#### Round 7  (e=3/3 block=0 draw=5 disc=0)

  - hand:
    - [0] `DEFEND_IRONCLAD` cost=1 canPlay=True target=Self
    - [1] `DEFEND_IRONCLAD` cost=1 canPlay=True target=Self
    - [2] `DEFEND_IRONCLAD` cost=1 canPlay=True target=Self
    - [3] `STRIKE_IRONCLAD` cost=1 canPlay=True target=AnyEnemy
    - [4] `STRIKE_IRONCLAD` cost=1 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `SLITHERING_STRANGLER` 30/55 block=5 → Debuff
  - player powers: CONSTRICT_POWER:9

  → play card [0]
  → play card [0]
  → play card [0]
  → end_turn → round transition
#### Round 8  (e=3/3 block=0 draw=0 disc=5)

  - hand:
    - [0] `STRIKE_IRONCLAD` cost=1 canPlay=True target=AnyEnemy
    - [1] `DEFEND_IRONCLAD` cost=1 canPlay=True target=Self
    - [2] `BASH` cost=2 canPlay=True target=AnyEnemy
    - [3] `STRIKE_IRONCLAD` cost=1 canPlay=True target=AnyEnemy
    - [4] `STRIKE_IRONCLAD` cost=1 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `SLITHERING_STRANGLER` 30/55 block=0 → Attack 12×1
  - player powers: CONSTRICT_POWER:12

  → play card [0] target=0
  → play card [0]
  → play card [1] target=0
  → end_turn → round transition
#### Round 9  (e=3/3 block=0 draw=5 disc=0)

  - hand:
    - [0] `DEFEND_IRONCLAD` cost=1 canPlay=True target=Self
    - [1] `STRIKE_IRONCLAD` cost=1 canPlay=True target=AnyEnemy
    - [2] `STRIKE_IRONCLAD` cost=1 canPlay=True target=AnyEnemy
    - [3] `STRIKE_IRONCLAD` cost=1 canPlay=True target=AnyEnemy
    - [4] `STRIKE_IRONCLAD` cost=1 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `SLITHERING_STRANGLER` 18/55 block=0 → Debuff
  - player powers: CONSTRICT_POWER:12

  → play card [0]
  → play card [0] target=0
  → play card [0] target=0
  → end_turn → round transition
#### Round 10  (e=3/3 block=0 draw=0 disc=5)

  - hand:
    - [0] `STRIKE_IRONCLAD` cost=1 canPlay=True target=AnyEnemy
    - [1] `DEFEND_IRONCLAD` cost=1 canPlay=True target=Self
    - [2] `DEFEND_IRONCLAD` cost=1 canPlay=True target=Self
    - [3] `DEFEND_IRONCLAD` cost=1 canPlay=True target=Self
    - [4] `BASH` cost=2 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `SLITHERING_STRANGLER` 6/55 block=0 → Attack 12×1
  - player powers: CONSTRICT_POWER:15

  → play card [0] target=0
  - rewards offered:
    - [0] gold 13g  canSkip=False
    - [1] potion potion=`POTION.COLORLESS_POTION (57870399)`  canSkip=False
    - [2] card cards=[`TRUE_GRIT`(cost=1), `SECOND_WIND`(cost=1), `ARMAMENTS`(cost=1)]  canSkip=True

  → claim reward [0] → hp=64 room=CombatRoom
  - rewards offered:
    - [0] potion potion=`POTION.COLORLESS_POTION (57870399)`  canSkip=False
    - [1] card cards=[`TRUE_GRIT`(cost=1), `SECOND_WIND`(cost=1), `ARMAMENTS`(cost=1)]  canSkip=True

  → claim reward [0] → hp=64 room=CombatRoom
  - rewards offered:
    - [0] card cards=[`TRUE_GRIT`(cost=1), `SECOND_WIND`(cost=1), `ARMAMENTS`(cost=1)]  canSkip=True

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
    - [0] `DEFEND_IRONCLAD` cost=1 canPlay=True target=Self
    - [1] `STRIKE_IRONCLAD` cost=1 canPlay=True target=AnyEnemy
    - [2] `STRIKE_IRONCLAD` cost=1 canPlay=True target=AnyEnemy
    - [3] `DEFEND_IRONCLAD` cost=1 canPlay=True target=Self
    - [4] `STRIKE_IRONCLAD` cost=1 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `FOGMOG` 74/74 block=0 → Unknown

  → play card [0]
  → play card [0] target=0
  → play card [0] target=0
  → end_turn → round transition
#### Round 2  (e=3/3 block=0 draw=0 disc=5)

  - hand:
    - [0] `DEFEND_IRONCLAD` cost=1 canPlay=True target=Self
    - [1] `STRIKE_IRONCLAD` cost=1 canPlay=True target=AnyEnemy
    - [2] `DEFEND_IRONCLAD` cost=1 canPlay=True target=Self
    - [3] `BASH` cost=2 canPlay=True target=AnyEnemy
    - [4] `STRIKE_IRONCLAD` cost=1 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `EYE_WITH_TEETH` 6/6 block=0 → Unknown  powers=[ILLUSION_POWER:1,MINION_POWER:1]
    - [1] `FOGMOG` 62/74 block=0 → Attack 8×1 + Buff

  → play card [0]
  → play card [0] target=0
  → play card [0]
  → end_turn → round transition
#### Round 3  (e=3/3 block=0 draw=5 disc=0)

  - hand:
    - [0] `STRIKE_IRONCLAD` cost=1 canPlay=True target=AnyEnemy
    - [1] `STRIKE_IRONCLAD` cost=1 canPlay=True target=AnyEnemy
    - [2] `STRIKE_IRONCLAD` cost=1 canPlay=True target=AnyEnemy
    - [3] `STRIKE_IRONCLAD` cost=1 canPlay=True target=AnyEnemy
    - [4] `BASH` cost=2 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `EYE_WITH_TEETH` 6/6 block=0 → Unknown  powers=[ILLUSION_POWER:1,MINION_POWER:1]
    - [1] `FOGMOG` 62/74 block=0 → Attack 8×1 + Buff  powers=[STRENGTH_POWER:1]

  → play card [0] target=0
  → play card [0] target=0
  → play card [0] target=0
  → end_turn → round transition
#### Round 4  (e=3/3 block=0 draw=0 disc=5)

  - hand:
    - [0] `DEFEND_IRONCLAD` cost=1 canPlay=True target=Self
    - [1] `STRIKE_IRONCLAD` cost=1 canPlay=True target=AnyEnemy
    - [2] `DEFEND_IRONCLAD` cost=1 canPlay=True target=Self
    - [3] `DEFEND_IRONCLAD` cost=1 canPlay=True target=Self
    - [4] `DEFEND_IRONCLAD` cost=1 canPlay=True target=Self
  - enemies:
    - [0] `EYE_WITH_TEETH` 6/6 block=0 → Unknown  powers=[ILLUSION_POWER:1,MINION_POWER:1]
    - [1] `FOGMOG` 50/74 block=0 → Attack 14×1  powers=[STRENGTH_POWER:2]

  → play card [0]
  → play card [0] target=0
  → play card [0]
  → end_turn → round transition
#### Round 5  (e=3/3 block=0 draw=5 disc=0)

  - hand:
    - [0] `STRIKE_IRONCLAD` cost=1 canPlay=True target=AnyEnemy
    - [1] `DEFEND_IRONCLAD` cost=1 canPlay=True target=Self
    - [2] `STRIKE_IRONCLAD` cost=1 canPlay=True target=AnyEnemy
    - [3] `STRIKE_IRONCLAD` cost=1 canPlay=True target=AnyEnemy
    - [4] `STRIKE_IRONCLAD` cost=1 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `EYE_WITH_TEETH` 6/6 block=0 → Unknown  powers=[ILLUSION_POWER:1,MINION_POWER:1]
    - [1] `FOGMOG` 50/74 block=0 → Attack 8×1 + Buff  powers=[STRENGTH_POWER:2]

  → play card [0] target=0
  → play card [0]
  → play card [0] target=0
  → end_turn → round transition
#### Round 6  (e=3/3 block=0 draw=0 disc=5)

  - hand:
    - [0] `STRIKE_IRONCLAD` cost=1 canPlay=True target=AnyEnemy
    - [1] `DEFEND_IRONCLAD` cost=1 canPlay=True target=Self
    - [2] `DEFEND_IRONCLAD` cost=1 canPlay=True target=Self
    - [3] `DEFEND_IRONCLAD` cost=1 canPlay=True target=Self
    - [4] `BASH` cost=2 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `EYE_WITH_TEETH` 6/6 block=0 → Unknown  powers=[ILLUSION_POWER:1,MINION_POWER:1]
    - [1] `FOGMOG` 44/74 block=0 → Attack 14×1  powers=[STRENGTH_POWER:3]

  → play card [0] target=0
  → play card [0]
  → play card [0]
  → end_turn → round transition
#### Round 7  (e=3/3 block=0 draw=5 disc=0)

  - hand:
    - [0] `DEFEND_IRONCLAD` cost=1 canPlay=True target=Self
    - [1] `STRIKE_IRONCLAD` cost=1 canPlay=True target=AnyEnemy
    - [2] `STRIKE_IRONCLAD` cost=1 canPlay=True target=AnyEnemy
    - [3] `BASH` cost=2 canPlay=True target=AnyEnemy
    - [4] `STRIKE_IRONCLAD` cost=1 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `EYE_WITH_TEETH` 6/6 block=0 → Unknown  powers=[ILLUSION_POWER:1,MINION_POWER:1]
    - [1] `FOGMOG` 44/74 block=0 → Attack 8×1 + Buff  powers=[STRENGTH_POWER:3]

  → play card [0]
  → play card [0] target=0
  → play card [0] target=0
  → end_turn → round transition
#### Round 8  (e=3/3 block=0 draw=0 disc=5)

  - hand:
    - [0] `STRIKE_IRONCLAD` cost=1 canPlay=True target=AnyEnemy
    - [1] `DEFEND_IRONCLAD` cost=1 canPlay=True target=Self
    - [2] `DEFEND_IRONCLAD` cost=1 canPlay=True target=Self
    - [3] `DEFEND_IRONCLAD` cost=1 canPlay=True target=Self
    - [4] `STRIKE_IRONCLAD` cost=1 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `EYE_WITH_TEETH` 6/6 block=0 → Unknown  powers=[ILLUSION_POWER:1,MINION_POWER:1]
    - [1] `FOGMOG` 38/74 block=0 → Attack 14×1  powers=[STRENGTH_POWER:4]

  → play card [0] target=0
  → play card [0]
  → play card [0]
  → end_turn → round transition
#### Round 9  (e=3/3 block=0 draw=5 disc=0)

  - hand:
    - [0] `STRIKE_IRONCLAD` cost=1 canPlay=True target=AnyEnemy
    - [1] `DEFEND_IRONCLAD` cost=1 canPlay=True target=Self
    - [2] `STRIKE_IRONCLAD` cost=1 canPlay=True target=AnyEnemy
    - [3] `DEFEND_IRONCLAD` cost=1 canPlay=True target=Self
    - [4] `STRIKE_IRONCLAD` cost=1 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `EYE_WITH_TEETH` 6/6 block=0 → Unknown  powers=[ILLUSION_POWER:1,MINION_POWER:1]
    - [1] `FOGMOG` 38/74 block=0 → Attack 8×1 + Buff  powers=[STRENGTH_POWER:4]

  → play card [0] target=0
  → play card [0]
  → play card [0] target=0
  → end_turn → round transition
#### Round 10  (e=3/3 block=0 draw=0 disc=5)

  - hand:
    - [0] `BASH` cost=2 canPlay=True target=AnyEnemy
    - [1] `DEFEND_IRONCLAD` cost=1 canPlay=True target=Self
    - [2] `STRIKE_IRONCLAD` cost=1 canPlay=True target=AnyEnemy
    - [3] `DEFEND_IRONCLAD` cost=1 canPlay=True target=Self
    - [4] `STRIKE_IRONCLAD` cost=1 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `EYE_WITH_TEETH` 6/6 block=0 → Unknown  powers=[ILLUSION_POWER:1,MINION_POWER:1]
    - [1] `FOGMOG` 32/74 block=0 → Attack 14×1  powers=[STRENGTH_POWER:5]

  → play card [0] target=0
  → play card [0]
  → end_turn → round transition
#### Round 11  (e=3/3 block=0 draw=5 disc=0)

  - hand:
    - [0] `STRIKE_IRONCLAD` cost=1 canPlay=True target=AnyEnemy
    - [1] `DEFEND_IRONCLAD` cost=1 canPlay=True target=Self
    - [2] `STRIKE_IRONCLAD` cost=1 canPlay=True target=AnyEnemy
    - [3] `STRIKE_IRONCLAD` cost=1 canPlay=True target=AnyEnemy
    - [4] `DEFEND_IRONCLAD` cost=1 canPlay=True target=Self
  - enemies:
    - [0] `EYE_WITH_TEETH` 6/6 block=0 → Unknown  powers=[ILLUSION_POWER:1,MINION_POWER:1]
    - [1] `FOGMOG` 32/74 block=0 → Attack 8×1 + Buff  powers=[STRENGTH_POWER:5]

  → play card [0] target=0
  → play card [0]
  → play card [0] target=0
  → end_turn → round transition
#### Round 12  (e=3/3 block=0 draw=0 disc=5)

  - hand:
    - [0] `STRIKE_IRONCLAD` cost=1 canPlay=True target=AnyEnemy
    - [1] `DEFEND_IRONCLAD` cost=1 canPlay=True target=Self
    - [2] `STRIKE_IRONCLAD` cost=1 canPlay=True target=AnyEnemy
    - [3] `BASH` cost=2 canPlay=True target=AnyEnemy
    - [4] `DEFEND_IRONCLAD` cost=1 canPlay=True target=Self
  - enemies:
    - [0] `EYE_WITH_TEETH` 6/6 block=0 → Unknown  powers=[ILLUSION_POWER:1,MINION_POWER:1]
    - [1] `FOGMOG` 26/74 block=0 → Attack 14×1  powers=[STRENGTH_POWER:6]

  → play card [0] target=0
  → play card [0]
  → play card [0] target=0
  → end_turn → round transition
  - rewards offered:
    - [0] gold 11g  canSkip=False
    - [1] potion potion=`POTION.BLOCK_POTION (22885177)`  canSkip=False
    - [2] card cards=[`TAUNT`(cost=1), `ARMAMENTS`(cost=1), `BLOOD_WALL`(cost=2)]  canSkip=True

  → claim reward [0] → hp=0 room=CombatRoom
  - rewards offered:
    - [0] potion potion=`POTION.BLOCK_POTION (22885177)`  canSkip=False
    - [1] card cards=[`TAUNT`(cost=1), `ARMAMENTS`(cost=1), `BLOOD_WALL`(cost=2)]  canSkip=True

  → claim reward [0] → hp=0 room=CombatRoom
  - rewards offered:
    - [0] card cards=[`TAUNT`(cost=1), `ARMAMENTS`(cost=1), `BLOOD_WALL`(cost=2)]  canSkip=True

## Floor 15: MapRoom  (hp=0)

  - map options: (0,15):RestSite

  → skip reward [0] → hp=0 room=MapRoom
  - combat ended (hp=0)

  - heal → hp=80/80

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
    - [0] `DEFEND_IRONCLAD` cost=1 canPlay=True target=Self
    - [1] `STRIKE_IRONCLAD` cost=1 canPlay=True target=AnyEnemy
    - [2] `STRIKE_IRONCLAD` cost=1 canPlay=True target=AnyEnemy
    - [3] `DEFEND_IRONCLAD` cost=1 canPlay=True target=Self
    - [4] `BASH` cost=2 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `VANTOM` 173/173 block=0 → Attack 7×1  powers=[SLIPPERY_POWER:9]

  - heal → hp=80/80

  → play card [0]
  → play card [0] target=0
  → play card [0] target=0
  → end_turn → round transition
#### Round 2  (e=3/3 block=0 draw=0 disc=5)

  - hand:
    - [0] `STRIKE_IRONCLAD` cost=1 canPlay=True target=AnyEnemy
    - [1] `DEFEND_IRONCLAD` cost=1 canPlay=True target=Self
    - [2] `STRIKE_IRONCLAD` cost=1 canPlay=True target=AnyEnemy
    - [3] `STRIKE_IRONCLAD` cost=1 canPlay=True target=AnyEnemy
    - [4] `DEFEND_IRONCLAD` cost=1 canPlay=True target=Self
  - enemies:
    - [0] `VANTOM` 171/173 block=0 → Attack 6×2  powers=[SLIPPERY_POWER:7]

  → play card [0] target=0
  → play card [0]
  → play card [0] target=0
  → end_turn → round transition
#### Round 3  (e=3/3 block=0 draw=5 disc=0)

  - hand:
    - [0] `DEFEND_IRONCLAD` cost=1 canPlay=True target=Self
    - [1] `DEFEND_IRONCLAD` cost=1 canPlay=True target=Self
    - [2] `DEFEND_IRONCLAD` cost=1 canPlay=True target=Self
    - [3] `BASH` cost=2 canPlay=True target=AnyEnemy
    - [4] `DEFEND_IRONCLAD` cost=1 canPlay=True target=Self
  - enemies:
    - [0] `VANTOM` 169/173 block=0 → Attack 27×1 + Unknown  powers=[SLIPPERY_POWER:5]

  → play card [0]
  → play card [0]
  → play card [0]
  → end_turn → round transition
#### Round 4  (e=3/3 block=0 draw=0 disc=5)

  - hand:
    - [0] `STRIKE_IRONCLAD` cost=1 canPlay=True target=AnyEnemy
    - [1] `STRIKE_IRONCLAD` cost=1 canPlay=True target=AnyEnemy
    - [2] `STRIKE_IRONCLAD` cost=1 canPlay=True target=AnyEnemy
    - [3] `STRIKE_IRONCLAD` cost=1 canPlay=True target=AnyEnemy
    - [4] `STRIKE_IRONCLAD` cost=1 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `VANTOM` 169/173 block=0 → Buff  powers=[SLIPPERY_POWER:5]

  → play card [0] target=0
  → play card [0] target=0
  → play card [0] target=0
  → end_turn → round transition
#### Round 5  (e=3/3 block=0 draw=5 disc=0)

  - hand:
    - [0] `DEFEND_IRONCLAD` cost=1 canPlay=True target=Self
    - [1] `DEFEND_IRONCLAD` cost=1 canPlay=True target=Self
    - [2] `STRIKE_IRONCLAD` cost=1 canPlay=True target=AnyEnemy
    - [3] `DEFEND_IRONCLAD` cost=1 canPlay=True target=Self
    - [4] `STRIKE_IRONCLAD` cost=1 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `VANTOM` 166/173 block=0 → Attack 7×1  powers=[SLIPPERY_POWER:2,STRENGTH_POWER:2]

  → play card [0]
  → play card [0]
  → play card [0] target=0
  → end_turn → round transition
#### Round 6  (e=3/3 block=0 draw=0 disc=5)

  - hand:
    - [0] `STRIKE_IRONCLAD` cost=1 canPlay=True target=AnyEnemy
    - [1] `STRIKE_IRONCLAD` cost=1 canPlay=True target=AnyEnemy
    - [2] `BASH` cost=2 canPlay=True target=AnyEnemy
    - [3] `STRIKE_IRONCLAD` cost=1 canPlay=True target=AnyEnemy
    - [4] `DEFEND_IRONCLAD` cost=1 canPlay=True target=Self
  - enemies:
    - [0] `VANTOM` 165/173 block=0 → Attack 6×2  powers=[SLIPPERY_POWER:1,STRENGTH_POWER:2]

  → play card [0] target=0
  → play card [0] target=0
  → play card [1] target=0
  → end_turn → round transition
#### Round 7  (e=3/3 block=0 draw=5 disc=0)

  - hand:
    - [0] `STRIKE_IRONCLAD` cost=1 canPlay=True target=AnyEnemy
    - [1] `STRIKE_IRONCLAD` cost=1 canPlay=True target=AnyEnemy
    - [2] `DEFEND_IRONCLAD` cost=1 canPlay=True target=Self
    - [3] `BASH` cost=2 canPlay=True target=AnyEnemy
    - [4] `STRIKE_IRONCLAD` cost=1 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `VANTOM` 152/173 block=0 → Attack 27×1 + Unknown  powers=[STRENGTH_POWER:2]

  → play card [0] target=0
  → play card [0] target=0
  → play card [0]
  → end_turn → round transition
#### Round 8  (e=3/3 block=0 draw=0 disc=5)

  - hand:
    - [0] `STRIKE_IRONCLAD` cost=1 canPlay=True target=AnyEnemy
    - [1] `DEFEND_IRONCLAD` cost=1 canPlay=True target=Self
    - [2] `STRIKE_IRONCLAD` cost=1 canPlay=True target=AnyEnemy
    - [3] `DEFEND_IRONCLAD` cost=1 canPlay=True target=Self
    - [4] `DEFEND_IRONCLAD` cost=1 canPlay=True target=Self
  - enemies:
    - [0] `VANTOM` 140/173 block=0 → Buff  powers=[STRENGTH_POWER:2]

  → play card [0] target=0
  → play card [0]
  → play card [0] target=0
  → end_turn → round transition
#### Round 9  (e=3/3 block=0 draw=5 disc=0)

  - hand:
    - [0] `BASH` cost=2 canPlay=True target=AnyEnemy
    - [1] `STRIKE_IRONCLAD` cost=1 canPlay=True target=AnyEnemy
    - [2] `STRIKE_IRONCLAD` cost=1 canPlay=True target=AnyEnemy
    - [3] `DEFEND_IRONCLAD` cost=1 canPlay=True target=Self
    - [4] `DEFEND_IRONCLAD` cost=1 canPlay=True target=Self
  - enemies:
    - [0] `VANTOM` 128/173 block=0 → Attack 7×1  powers=[STRENGTH_POWER:4]

  → play card [0] target=0
  → play card [0] target=0
  → end_turn → round transition
#### Round 10  (e=3/3 block=0 draw=0 disc=5)

  - hand:
    - [0] `STRIKE_IRONCLAD` cost=1 canPlay=True target=AnyEnemy
    - [1] `DEFEND_IRONCLAD` cost=1 canPlay=True target=Self
    - [2] `STRIKE_IRONCLAD` cost=1 canPlay=True target=AnyEnemy
    - [3] `DEFEND_IRONCLAD` cost=1 canPlay=True target=Self
    - [4] `STRIKE_IRONCLAD` cost=1 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `VANTOM` 111/173 block=0 → Attack 6×2  powers=[STRENGTH_POWER:4,VULNERABLE_POWER:1]

  → play card [0] target=0
  → play card [0]
  → play card [0] target=0
  → end_turn → round transition
#### Round 11  (e=3/3 block=0 draw=5 disc=0)

  - hand:
    - [0] `DEFEND_IRONCLAD` cost=1 canPlay=True target=Self
    - [1] `STRIKE_IRONCLAD` cost=1 canPlay=True target=AnyEnemy
    - [2] `BASH` cost=2 canPlay=True target=AnyEnemy
    - [3] `DEFEND_IRONCLAD` cost=1 canPlay=True target=Self
    - [4] `STRIKE_IRONCLAD` cost=1 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `VANTOM` 93/173 block=0 → Attack 27×1 + Unknown  powers=[STRENGTH_POWER:4]

  → play card [0]
  → play card [0] target=0
  → play card [1]
  → end_turn → round transition
#### Round 12  (e=3/3 block=0 draw=0 disc=5)

  - hand:
    - [0] `STRIKE_IRONCLAD` cost=1 canPlay=True target=AnyEnemy
    - [1] `STRIKE_IRONCLAD` cost=1 canPlay=True target=AnyEnemy
    - [2] `DEFEND_IRONCLAD` cost=1 canPlay=True target=Self
    - [3] `DEFEND_IRONCLAD` cost=1 canPlay=True target=Self
    - [4] `STRIKE_IRONCLAD` cost=1 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `VANTOM` 87/173 block=0 → Buff  powers=[STRENGTH_POWER:4]

  → play card [0] target=0
  → play card [0] target=0
  → play card [0]
  → end_turn → round transition
#### Round 13  (e=3/3 block=0 draw=5 disc=0)

  - hand:
    - [0] `DEFEND_IRONCLAD` cost=1 canPlay=True target=Self
    - [1] `DEFEND_IRONCLAD` cost=1 canPlay=True target=Self
    - [2] `STRIKE_IRONCLAD` cost=1 canPlay=True target=AnyEnemy
    - [3] `STRIKE_IRONCLAD` cost=1 canPlay=True target=AnyEnemy
    - [4] `BASH` cost=2 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `VANTOM` 75/173 block=0 → Attack 7×1  powers=[STRENGTH_POWER:6]

  → play card [0]
  → play card [0]
  → play card [0] target=0
  → end_turn → round transition
#### Round 14  (e=3/3 block=0 draw=0 disc=5)

  - hand:
    - [0] `DEFEND_IRONCLAD` cost=1 canPlay=True target=Self
    - [1] `STRIKE_IRONCLAD` cost=1 canPlay=True target=AnyEnemy
    - [2] `DEFEND_IRONCLAD` cost=1 canPlay=True target=Self
    - [3] `STRIKE_IRONCLAD` cost=1 canPlay=True target=AnyEnemy
    - [4] `STRIKE_IRONCLAD` cost=1 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `VANTOM` 69/173 block=0 → Attack 6×2  powers=[STRENGTH_POWER:6]

  → play card [0]
  → play card [0] target=0
  → play card [0]
  → end_turn → round transition
#### Round 15  (e=3/3 block=0 draw=5 disc=0)

  - hand:
    - [0] `STRIKE_IRONCLAD` cost=1 canPlay=True target=AnyEnemy
    - [1] `STRIKE_IRONCLAD` cost=1 canPlay=True target=AnyEnemy
    - [2] `BASH` cost=2 canPlay=True target=AnyEnemy
    - [3] `DEFEND_IRONCLAD` cost=1 canPlay=True target=Self
    - [4] `STRIKE_IRONCLAD` cost=1 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `VANTOM` 63/173 block=0 → Attack 27×1 + Unknown  powers=[STRENGTH_POWER:6]

  → play card [0] target=0
  → play card [0] target=0
  → play card [1]
  → end_turn → round transition
#### Round 16  (e=3/3 block=0 draw=0 disc=5)

  - hand:
    - [0] `DEFEND_IRONCLAD` cost=1 canPlay=True target=Self
    - [1] `STRIKE_IRONCLAD` cost=1 canPlay=True target=AnyEnemy
    - [2] `STRIKE_IRONCLAD` cost=1 canPlay=True target=AnyEnemy
    - [3] `DEFEND_IRONCLAD` cost=1 canPlay=True target=Self
    - [4] `DEFEND_IRONCLAD` cost=1 canPlay=True target=Self
  - enemies:
    - [0] `VANTOM` 51/173 block=0 → Buff  powers=[STRENGTH_POWER:6]

  → play card [0]
  → play card [0] target=0
  → play card [0] target=0
  → end_turn → round transition
#### Round 17  (e=3/3 block=0 draw=5 disc=0)

  - hand:
    - [0] `DEFEND_IRONCLAD` cost=1 canPlay=True target=Self
    - [1] `DEFEND_IRONCLAD` cost=1 canPlay=True target=Self
    - [2] `BASH` cost=2 canPlay=True target=AnyEnemy
    - [3] `STRIKE_IRONCLAD` cost=1 canPlay=True target=AnyEnemy
    - [4] `STRIKE_IRONCLAD` cost=1 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `VANTOM` 39/173 block=0 → Attack 7×1  powers=[STRENGTH_POWER:8]

  → play card [0]
  → play card [0]
  → play card [1] target=0
  → end_turn → round transition
#### Round 18  (e=3/3 block=0 draw=0 disc=5)

  - hand:
    - [0] `DEFEND_IRONCLAD` cost=1 canPlay=True target=Self
    - [1] `DEFEND_IRONCLAD` cost=1 canPlay=True target=Self
    - [2] `STRIKE_IRONCLAD` cost=1 canPlay=True target=AnyEnemy
    - [3] `STRIKE_IRONCLAD` cost=1 canPlay=True target=AnyEnemy
    - [4] `STRIKE_IRONCLAD` cost=1 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `VANTOM` 33/173 block=0 → Attack 6×2  powers=[STRENGTH_POWER:8]

  → play card [0]
  → play card [0]
  → play card [0] target=0
  → end_turn → round transition
  - rewards offered:
    - [0] gold 100g  canSkip=False
    - [1] card cards=[`CRIMSON_MANTLE`(cost=1), `CONFLAGRATION`(cost=1), `STOKE`(cost=1)]  canSkip=True

  → claim reward [0] → hp=0 room=BossRoom
  - rewards offered:
    - [0] card cards=[`CRIMSON_MANTLE`(cost=1), `CONFLAGRATION`(cost=1), `STOKE`(cost=1)]  canSkip=True

## Floor 17: MapRoom  (hp=0)


  → skip reward [0] → hp=0 room=MapRoom
  - combat ended (hp=0)

  - heal → hp=80/80

