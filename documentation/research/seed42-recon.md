# Seed 42 Recon — Ironclad

- character: `Ironclad`
- seed: `42`
- starting relics: `BurningBlood`

## Floor 1: EventRoom  (hp=80/80)

  - event options:
    - [0] `NEOW.pages.INITIAL.options.ARCANE_SCROLL`
    - [1] `NEOW.pages.INITIAL.options.PHIAL_HOLSTER`
    - [2] `NEOW.pages.INITIAL.options.SCROLL_BOXES`

  - bag: (empty)
## Floor 1: MapRoom  (hp=80)

  - map options: (0,1):Monster, (3,1):Monster, (5,1):Monster

  → pick event option [2] → MapRoom hp=80
## Floor 2: CombatRoom  (hp=80)


  → pick map (0,1) → CombatRoom floor=2
### Combat #1 on floor 2

#### Round 1  (e=3/3 block=0 draw=8 disc=0)

  - hand:
    - [0] `DefendIronclad` cost=1 canPlay=True target=Self
    - [1] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [2] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [3] `Bash` cost=2 canPlay=True target=AnyEnemy
    - [4] `Rage` cost=0 canPlay=True target=Self
  - enemies:
    - [0] `FuzzyWurmCrawler` 55/55 block=0 → Attack 4×1

  → play card [0]
  → play card [0] target=0
  → play card [0] target=0
  → play card [1]
  → end_turn → round transition
#### Round 2  (e=3/3 block=0 draw=3 disc=5)

  - hand:
    - [0] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [1] `Bloodletting` cost=0 canPlay=True target=Self
    - [2] `BodySlam` cost=1 canPlay=True target=AnyEnemy
    - [3] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [4] `DefendIronclad` cost=1 canPlay=True target=Self
  - enemies:
    - [0] `FuzzyWurmCrawler` 43/55 block=0 → Buff

  → play card [0] target=0
  → play card [0]
  → play card [0] target=0
  → play card [0] target=0
  → play card [0]
  → end_turn → round transition
#### Round 3  (e=3/3 block=0 draw=8 disc=0)

  - hand:
    - [0] `DefendIronclad` cost=1 canPlay=True target=Self
    - [1] `DefendIronclad` cost=1 canPlay=True target=Self
    - [2] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [3] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [4] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `FuzzyWurmCrawler` 31/55 block=0 → Attack 4×1  powers=[StrengthPower:7]

  → play card [0]
  → play card [0]
  → play card [0] target=0
  → end_turn → round transition
#### Round 4  (e=3/3 block=0 draw=3 disc=5)

  - hand:
    - [0] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [1] `DefendIronclad` cost=1 canPlay=True target=Self
    - [2] `DefendIronclad` cost=1 canPlay=True target=Self
    - [3] `Bash` cost=2 canPlay=True target=AnyEnemy
    - [4] `BodySlam` cost=1 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `FuzzyWurmCrawler` 25/55 block=0 → Attack 4×1  powers=[StrengthPower:7]

  → play card [0] target=0
  → play card [0]
  → play card [0]
  → end_turn → round transition
#### Round 5  (e=3/3 block=0 draw=8 disc=0)

  - hand:
    - [0] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [1] `Rage` cost=0 canPlay=True target=Self
    - [2] `Bloodletting` cost=0 canPlay=True target=Self
    - [3] `DefendIronclad` cost=1 canPlay=True target=Self
    - [4] `DefendIronclad` cost=1 canPlay=True target=Self
  - enemies:
    - [0] `FuzzyWurmCrawler` 19/55 block=0 → Buff  powers=[StrengthPower:7]

  → play card [0] target=0
  → play card [0]
  → play card [0]
  → play card [0]
  → play card [0]
  → end_turn → round transition
#### Round 6  (e=3/3 block=0 draw=3 disc=5)

  - hand:
    - [0] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [1] `BodySlam` cost=1 canPlay=True target=AnyEnemy
    - [2] `Bash` cost=2 canPlay=True target=AnyEnemy
    - [3] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [4] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `FuzzyWurmCrawler` 13/55 block=0 → Attack 4×1  powers=[StrengthPower:14]

  → play card [0] target=0
  → play card [0] target=0
  → play card [1] target=0
  → end_turn → round transition
#### Round 7  (e=3/3 block=0 draw=8 disc=0)

  - hand:
    - [0] `DefendIronclad` cost=1 canPlay=True target=Self
    - [1] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [2] `DefendIronclad` cost=1 canPlay=True target=Self
    - [3] `Rage` cost=0 canPlay=True target=Self
    - [4] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `FuzzyWurmCrawler` 1/55 block=0 → Attack 4×1  powers=[StrengthPower:14]

  → play card [0]
  → play card [0] target=0
  - rewards offered:
    - [0] gold 19g  canSkip=False
    - [1] potion potion=`DexterityPotion`  canSkip=False
    - [2] card cards=[`Thunderclap`(cost=1), `Armaments`(cost=1), `TrueGrit`(cost=1)]  canSkip=True

  - rewards offered:
    - [0] gold 19g  canSkip=False
    - [1] potion potion=`DexterityPotion`  canSkip=False
    - [2] card cards=[`Thunderclap`(cost=1), `Armaments`(cost=1), `TrueGrit`(cost=1)]  canSkip=True

  → claim reward [0] → hp=60 room=CombatRoom
  - rewards offered:
    - [0] potion potion=`DexterityPotion`  canSkip=False
    - [1] card cards=[`Thunderclap`(cost=1), `Armaments`(cost=1), `TrueGrit`(cost=1)]  canSkip=True

  - rewards offered:
    - [0] potion potion=`DexterityPotion`  canSkip=False
    - [1] card cards=[`Thunderclap`(cost=1), `Armaments`(cost=1), `TrueGrit`(cost=1)]  canSkip=True

  → claim reward [0] → hp=60 room=CombatRoom
  - bag: [0]DexterityPotion/Unknown
  - rewards offered:
    - [0] card cards=[`Thunderclap`(cost=1), `Armaments`(cost=1), `TrueGrit`(cost=1)]  canSkip=True

  - rewards offered:
    - [0] card cards=[`Thunderclap`(cost=1), `Armaments`(cost=1), `TrueGrit`(cost=1)]  canSkip=True

## Floor 2: MapRoom  (hp=60)

  - map options: (0,2):Unknown

  → skip reward [0] → hp=60 room=MapRoom
  - combat ended (hp=60)

  - heal → hp=80/80

## Floor 3: EventRoom  (hp=80)

  - event options:
    - [0] `THE_LEGENDS_WERE_TRUE.pages.INITIAL.options.NAB_THE_MAP`
    - [1] `THE_LEGENDS_WERE_TRUE.pages.INITIAL.options.SLOWLY_FIND_AN_EXIT`

  → pick map (0,2) → EventRoom floor=3
## Floor 3: MapRoom  (hp=72)

  - map options: (0,3):Monster

  → pick event option [1] → MapRoom hp=72
  - bag: [0]DexterityPotion/Unknown,[1]Duplicator/Self
  - heal → hp=80/80

## Floor 4: CombatRoom  (hp=80)


  → pick map (0,3) → CombatRoom floor=4
### Combat #1 on floor 4

#### Round 1  (e=3/3 block=0 draw=8 disc=0)

  - hand:
    - [0] `DefendIronclad` cost=1 canPlay=True target=Self
    - [1] `Bash` cost=2 canPlay=True target=AnyEnemy
    - [2] `DefendIronclad` cost=1 canPlay=True target=Self
    - [3] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [4] `Rage` cost=0 canPlay=True target=Self
  - enemies:
    - [0] `ShrinkerBeetle` 39/39 block=0 → Unknown

  → play card [0]
  → play card [0] target=0
  → play card [2]
  → end_turn → round transition
#### Round 2  (e=3/3 block=0 draw=3 disc=5)

  - hand:
    - [0] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [1] `DefendIronclad` cost=1 canPlay=True target=Self
    - [2] `BodySlam` cost=1 canPlay=True target=AnyEnemy
    - [3] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [4] `DefendIronclad` cost=1 canPlay=True target=Self
  - enemies:
    - [0] `ShrinkerBeetle` 31/39 block=0 → Attack 7×1  powers=[VulnerablePower:1]
  - player powers: ShrinkPower:-1

  → play card [0] target=0
  → play card [0]
  → play card [0] target=0
  → end_turn → round transition
#### Round 3  (e=3/3 block=0 draw=8 disc=0)

  - hand:
    - [0] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [1] `Bloodletting` cost=0 canPlay=True target=Self
    - [2] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [3] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [4] `DefendIronclad` cost=1 canPlay=True target=Self
  - enemies:
    - [0] `ShrinkerBeetle` 20/39 block=0 → Attack 13×1
  - player powers: ShrinkPower:-1

  → play card [0] target=0
  → play card [0]
  → play card [0] target=0
  → play card [0] target=0
  → play card [0]
  → end_turn → round transition
#### Round 4  (e=3/3 block=0 draw=3 disc=5)

  - hand:
    - [0] `Bash` cost=2 canPlay=True target=AnyEnemy
    - [1] `Rage` cost=0 canPlay=True target=Self
    - [2] `DefendIronclad` cost=1 canPlay=True target=Self
    - [3] `DefendIronclad` cost=1 canPlay=True target=Self
    - [4] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `ShrinkerBeetle` 8/39 block=0 → Attack 7×1
  - player powers: ShrinkPower:-1

  → play card [0] target=0
  → play card [0]
  → play card [0]
  → end_turn → round transition
#### Round 5  (e=3/3 block=0 draw=8 disc=0)

  - hand:
    - [0] `DefendIronclad` cost=1 canPlay=True target=Self
    - [1] `BodySlam` cost=1 canPlay=True target=AnyEnemy
    - [2] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [3] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [4] `DefendIronclad` cost=1 canPlay=True target=Self
  - enemies:
    - [0] `ShrinkerBeetle` 3/39 block=0 → Attack 13×1  powers=[VulnerablePower:1]
  - player powers: ShrinkPower:-1

  → play card [0]
  → play card [0] target=0
  - rewards offered:
    - [0] gold 13g  canSkip=False
    - [1] potion potion=`StableSerum`  canSkip=False
    - [2] card cards=[`Anger`(cost=0), `StoneArmor`(cost=1), `Havoc`(cost=1)]  canSkip=True

  - rewards offered:
    - [0] gold 13g  canSkip=False
    - [1] potion potion=`StableSerum`  canSkip=False
    - [2] card cards=[`Anger`(cost=0), `StoneArmor`(cost=1), `Havoc`(cost=1)]  canSkip=True

  → claim reward [0] → hp=71 room=CombatRoom
  - rewards offered:
    - [0] potion potion=`StableSerum`  canSkip=False
    - [1] card cards=[`Anger`(cost=0), `StoneArmor`(cost=1), `Havoc`(cost=1)]  canSkip=True

  - rewards offered:
    - [0] potion potion=`StableSerum`  canSkip=False
    - [1] card cards=[`Anger`(cost=0), `StoneArmor`(cost=1), `Havoc`(cost=1)]  canSkip=True

  → claim reward [0] → hp=71 room=CombatRoom
  - bag: [0]DexterityPotion/Unknown,[1]Duplicator/Self,[2]StableSerum/Unknown
  - rewards offered:
    - [0] card cards=[`Anger`(cost=0), `StoneArmor`(cost=1), `Havoc`(cost=1)]  canSkip=True

  - rewards offered:
    - [0] card cards=[`Anger`(cost=0), `StoneArmor`(cost=1), `Havoc`(cost=1)]  canSkip=True

## Floor 4: MapRoom  (hp=71)

  - map options: (0,4):Monster

  → skip reward [0] → hp=71 room=MapRoom
  - combat ended (hp=71)

  - heal → hp=80/80

## Floor 5: CombatRoom  (hp=80)


  → pick map (0,4) → CombatRoom floor=5
### Combat #1 on floor 5

#### Round 1  (e=3/3 block=0 draw=8 disc=0)

  - hand:
    - [0] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [1] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [2] `DefendIronclad` cost=1 canPlay=True target=Self
    - [3] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [4] `Bloodletting` cost=0 canPlay=True target=Self
  - enemies:
    - [0] `Nibbit` 44/44 block=0 → Attack 12×1

  → play card [0] target=0
  → play card [0] target=0
  → play card [0]
  → play card [1]
  → play card [0] target=0
  → end_turn → round transition
#### Round 2  (e=3/3 block=0 draw=3 disc=5)

  - hand:
    - [0] `DefendIronclad` cost=1 canPlay=True target=Self
    - [1] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [2] `DefendIronclad` cost=1 canPlay=True target=Self
    - [3] `DefendIronclad` cost=1 canPlay=True target=Self
    - [4] `Rage` cost=0 canPlay=True target=Self
  - enemies:
    - [0] `Nibbit` 26/44 block=0 → Attack 6×1 + Defend

  → play card [0]
  → play card [0] target=0
  → play card [0]
  → play card [1]
  → end_turn → round transition
#### Round 3  (e=3/3 block=0 draw=8 disc=0)

  - hand:
    - [0] `BodySlam` cost=1 canPlay=True target=AnyEnemy
    - [1] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [2] `Bash` cost=2 canPlay=True target=AnyEnemy
    - [3] `DefendIronclad` cost=1 canPlay=True target=Self
    - [4] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `Nibbit` 20/44 block=5 → Buff

  → play card [0] target=0
  → play card [0] target=0
  → play card [1]
  → end_turn → round transition
#### Round 4  (e=3/3 block=0 draw=3 disc=5)

  - hand:
    - [0] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [1] `Rage` cost=0 canPlay=True target=Self
    - [2] `DefendIronclad` cost=1 canPlay=True target=Self
    - [3] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [4] `Bloodletting` cost=0 canPlay=True target=Self
  - enemies:
    - [0] `Nibbit` 19/44 block=0 → Attack 12×1  powers=[StrengthPower:2]

  → play card [0] target=0
  → play card [0]
  → play card [0]
  → play card [0] target=0
  → play card [0]
  → end_turn → round transition
#### Round 5  (e=3/3 block=0 draw=8 disc=0)

  - hand:
    - [0] `DefendIronclad` cost=1 canPlay=True target=Self
    - [1] `DefendIronclad` cost=1 canPlay=True target=Self
    - [2] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [3] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [4] `DefendIronclad` cost=1 canPlay=True target=Self
  - enemies:
    - [0] `Nibbit` 7/44 block=0 → Attack 6×1 + Defend  powers=[StrengthPower:2]

  → play card [0]
  → play card [0]
  → play card [0] target=0
  → end_turn → round transition
#### Round 6  (e=3/3 block=0 draw=3 disc=5)

  - hand:
    - [0] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [1] `Bloodletting` cost=0 canPlay=True target=Self
    - [2] `Rage` cost=0 canPlay=True target=Self
    - [3] `Bash` cost=2 canPlay=True target=AnyEnemy
    - [4] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `Nibbit` 1/44 block=5 → Buff  powers=[StrengthPower:2]

  → play card [0] target=0
  - rewards offered:
    - [0] gold 11g  canSkip=False
    - [1] potion potion=`VulnerablePotion`  canSkip=False
    - [2] card cards=[`Cinder`(cost=2), `BloodWall`(cost=2), `Stampede`(cost=2)]  canSkip=True

  - rewards offered:
    - [0] gold 11g  canSkip=False
    - [1] potion potion=`VulnerablePotion`  canSkip=False
    - [2] card cards=[`Cinder`(cost=2), `BloodWall`(cost=2), `Stampede`(cost=2)]  canSkip=True

  → claim reward [0] → hp=67 room=CombatRoom
  - rewards offered:
    - [0] potion potion=`VulnerablePotion`  canSkip=False
    - [1] card cards=[`Cinder`(cost=2), `BloodWall`(cost=2), `Stampede`(cost=2)]  canSkip=True

  - rewards offered:
    - [0] potion potion=`VulnerablePotion`  canSkip=False
    - [1] card cards=[`Cinder`(cost=2), `BloodWall`(cost=2), `Stampede`(cost=2)]  canSkip=True

  → claim reward [0] → hp=67 room=CombatRoom
  - rewards offered:
    - [0] card cards=[`Cinder`(cost=2), `BloodWall`(cost=2), `Stampede`(cost=2)]  canSkip=True

  - rewards offered:
    - [0] card cards=[`Cinder`(cost=2), `BloodWall`(cost=2), `Stampede`(cost=2)]  canSkip=True

## Floor 5: MapRoom  (hp=67)

  - map options: (0,5):Unknown

  → skip reward [0] → hp=67 room=MapRoom
  - combat ended (hp=67)

  - heal → hp=80/80

## Floor 6: EventRoom  (hp=80)

  - event options:
    - [0] `JUNGLE_MAZE_ADVENTURE.pages.INITIAL.options.SOLO_QUEST`
    - [1] `JUNGLE_MAZE_ADVENTURE.pages.INITIAL.options.JOIN_FORCES`

  → pick map (0,5) → EventRoom floor=6
## Floor 6: MapRoom  (hp=80)

  - map options: (0,6):RestSite

  → pick event option [1] → MapRoom hp=80
## Floor 7: RestSiteRoom  (hp=80)


  → pick map (0,6) → RestSiteRoom floor=7
## Floor 7: MapRoom  (hp=80)

  - map options: (0,7):Elite

  → pick rest option [1] → MapRoom hp=80
## Floor 8: CombatRoom  (hp=80)


  → pick map (0,7) → CombatRoom floor=8
### Combat #1 on floor 8

#### Round 1  (e=3/3 block=0 draw=8 disc=0)

  - hand:
    - [0] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [1] `DefendIronclad` cost=1 canPlay=True target=Self
    - [2] `DefendIronclad` cost=1 canPlay=True target=Self
    - [3] `Bloodletting` cost=0 canPlay=True target=Self
    - [4] `DefendIronclad` cost=1 canPlay=True target=Self
  - enemies:
    - [0] `PhrogParasite` 64/64 block=0 → Unknown  powers=[InfestedPower:4]

  → play card [0] target=0
  → play card [0]
  → play card [0]
  → play card [0]
  → play card [0]
  → end_turn → round transition
#### Round 2  (e=3/3 block=0 draw=3 disc=8)

  - hand:
    - [0] `Bash` cost=2 canPlay=True target=AnyEnemy
    - [1] `BodySlam` cost=1 canPlay=True target=AnyEnemy
    - [2] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [3] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [4] `Rage` cost=0 canPlay=True target=Self
  - enemies:
    - [0] `PhrogParasite` 58/64 block=0 → Attack 4×4  powers=[InfestedPower:4]

  → play card [0] target=0
  → play card [0] target=0
  → play card [2]
  → end_turn → round transition
#### Round 3  (e=3/3 block=0 draw=11 disc=0)

  - hand:
    - [0] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [1] `DefendIronclad` cost=1 canPlay=True target=Self
    - [2] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [3] `DefendIronclad` cost=1 canPlay=True target=Self
    - [4] `Infection` cost=0 canPlay=False target=None
  - enemies:
    - [0] `PhrogParasite` 50/64 block=0 → Unknown  powers=[InfestedPower:4,VulnerablePower:1]

  → play card [0] target=0
  → play card [0]
  → play card [0] target=0
  → end_turn → round transition
#### Round 4  (e=3/3 block=0 draw=6 disc=8)

  - hand:
    - [0] `DefendIronclad` cost=1 canPlay=True target=Self
    - [1] `Rage` cost=0 canPlay=True target=Self
    - [2] `Bloodletting` cost=0 canPlay=True target=Self
    - [3] `Infection` cost=0 canPlay=False target=None
    - [4] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `PhrogParasite` 28/64 block=0 → Attack 4×4  powers=[InfestedPower:4]

  → play card [0]
  → play card [0]
  → play card [0]
  → play card [1] target=0
  → end_turn → round transition
#### Round 5  (e=3/3 block=0 draw=1 disc=13)

  - hand:
    - [0] `Infection` cost=0 canPlay=False target=None
    - [1] `DefendIronclad` cost=1 canPlay=True target=Self
    - [2] `Bash` cost=2 canPlay=True target=AnyEnemy
    - [3] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [4] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `PhrogParasite` 22/64 block=0 → Unknown  powers=[InfestedPower:4]

  → play card [1]
  → play card [1] target=0
  → end_turn → round transition
#### Round 6  (e=3/3 block=0 draw=17 disc=0)

  - hand:
    - [0] `BodySlam` cost=1 canPlay=True target=AnyEnemy
    - [1] `Infection` cost=0 canPlay=False target=None
    - [2] `Infection` cost=0 canPlay=False target=None
    - [3] `DefendIronclad` cost=1 canPlay=True target=Self
    - [4] `Infection` cost=0 canPlay=False target=None
  - enemies:
    - [0] `PhrogParasite` 14/64 block=0 → Attack 4×4  powers=[InfestedPower:4,VulnerablePower:1]

  → play card [0] target=0
  → play card [2]
  → end_turn → round transition
#### Round 7  (e=3/3 block=0 draw=12 disc=5)

  - hand:
    - [0] `Infection` cost=0 canPlay=False target=None
    - [1] `DefendIronclad` cost=1 canPlay=True target=Self
    - [2] `Bloodletting` cost=0 canPlay=True target=Self
    - [3] `DefendIronclad` cost=1 canPlay=True target=Self
    - [4] `Rage` cost=0 canPlay=True target=Self
  - enemies:
    - [0] `PhrogParasite` 14/64 block=0 → Unknown  powers=[InfestedPower:4]

  → play card [1]
  → play card [1]
  → play card [1]
  → play card [1]
  → end_turn → round transition
#### Round 8  (e=3/3 block=0 draw=7 disc=13)

  - hand:
    - [0] `Infection` cost=0 canPlay=False target=None
    - [1] `DefendIronclad` cost=1 canPlay=True target=Self
    - [2] `Bash` cost=2 canPlay=True target=AnyEnemy
    - [3] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [4] `Infection` cost=0 canPlay=False target=None
  - enemies:
    - [0] `PhrogParasite` 14/64 block=0 → Attack 4×4  powers=[InfestedPower:4]

  → play card [1]
  → play card [1] target=0
  → end_turn → round transition
#### Round 9  (e=3/3 block=0 draw=2 disc=18)

  - hand:
    - [0] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [1] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [2] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [3] `Infection` cost=0 canPlay=False target=None
    - [4] `Infection` cost=0 canPlay=False target=None
  - enemies:
    - [0] `PhrogParasite` 6/64 block=0 → Unknown  powers=[InfestedPower:4,VulnerablePower:1]

  → play card [0] target=0
  → play card [0] target=0
  → play card [0] target=0
  → end_turn → round transition
#### Round 10  (e=3/3 block=0 draw=20 disc=0)

  - hand:
    - [0] `Infection` cost=0 canPlay=False target=None
    - [1] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [2] `Infection` cost=0 canPlay=False target=None
    - [3] `DefendIronclad` cost=1 canPlay=True target=Self
    - [4] `Infection` cost=0 canPlay=False target=None
  - enemies:
    - [0] `Wriggler` 7/19 block=0 → Attack 6×1
    - [1] `Wriggler` 21/21 block=0 → Buff + Unknown
    - [2] `Wriggler` 18/18 block=0 → Attack 6×1
    - [3] `Wriggler` 17/17 block=0 → Buff + Unknown

  → play card [1] target=0
  → play card [2]
  → end_turn → round transition
  - rewards offered:
    - [0] gold 35g  canSkip=False
    - [1] potion potion=`RadiantTincture`  canSkip=False
    - [2] relic relic=``  canSkip=False
    - [3] card cards=[`Feed`(cost=1), `Breakthrough`(cost=1), `PactsEnd`(cost=0)]  canSkip=True

  - rewards offered:
    - [0] gold 35g  canSkip=False
    - [1] potion potion=`RadiantTincture`  canSkip=False
    - [2] relic relic=``  canSkip=False
    - [3] card cards=[`Feed`(cost=1), `Breakthrough`(cost=1), `PactsEnd`(cost=0)]  canSkip=True

  - heal → hp=80/80

  - rewards offered:
    - [0] gold 35g  canSkip=False
    - [1] potion potion=`RadiantTincture`  canSkip=False
    - [2] relic relic=``  canSkip=False
    - [3] card cards=[`Feed`(cost=1), `Breakthrough`(cost=1), `PactsEnd`(cost=0)]  canSkip=True

  → claim reward [0] → hp=80 room=CombatRoom
  - rewards offered:
    - [0] potion potion=`RadiantTincture`  canSkip=False
    - [1] relic relic=``  canSkip=False
    - [2] card cards=[`Feed`(cost=1), `Breakthrough`(cost=1), `PactsEnd`(cost=0)]  canSkip=True

  - rewards offered:
    - [0] potion potion=`RadiantTincture`  canSkip=False
    - [1] relic relic=``  canSkip=False
    - [2] card cards=[`Feed`(cost=1), `Breakthrough`(cost=1), `PactsEnd`(cost=0)]  canSkip=True

  → claim reward [0] → hp=80 room=CombatRoom
  - rewards offered:
    - [0] relic relic=``  canSkip=False
    - [1] card cards=[`Feed`(cost=1), `Breakthrough`(cost=1), `PactsEnd`(cost=0)]  canSkip=True

  - rewards offered:
    - [0] relic relic=``  canSkip=False
    - [1] card cards=[`Feed`(cost=1), `Breakthrough`(cost=1), `PactsEnd`(cost=0)]  canSkip=True

  → claim reward [0] → hp=80 room=CombatRoom
  - rewards offered:
    - [0] card cards=[`Feed`(cost=1), `Breakthrough`(cost=1), `PactsEnd`(cost=0)]  canSkip=True

  - rewards offered:
    - [0] card cards=[`Feed`(cost=1), `Breakthrough`(cost=1), `PactsEnd`(cost=0)]  canSkip=True

## Floor 8: MapRoom  (hp=80)

  - map options: (0,8):Monster

  → skip reward [0] → hp=80 room=MapRoom
  - combat ended (hp=80)

## Floor 9: CombatRoom  (hp=80)


  → pick map (0,8) → CombatRoom floor=9
### Combat #1 on floor 9

#### Round 1  (e=3/3 block=0 draw=8 disc=0)

  - hand:
    - [0] `DefendIronclad` cost=1 canPlay=True target=Self
    - [1] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [2] `BodySlam` cost=1 canPlay=True target=AnyEnemy
    - [3] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [4] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `Mawler` 72/72 block=0 → Attack 4×2

  → play card [0]
  → play card [0] target=0
  → play card [0] target=0
  → end_turn → round transition
#### Round 2  (e=3/3 block=0 draw=3 disc=5)

  - hand:
    - [0] `DefendIronclad` cost=1 canPlay=True target=Self
    - [1] `Bash` cost=2 canPlay=True target=AnyEnemy
    - [2] `Bloodletting` cost=0 canPlay=True target=Self
    - [3] `DefendIronclad` cost=1 canPlay=True target=Self
    - [4] `Rage` cost=0 canPlay=True target=Self
  - enemies:
    - [0] `Mawler` 61/72 block=0 → Attack 14×1

  → play card [0]
  → play card [0] target=0
  → play card [0]
  → play card [0]
  → play card [0]
  → end_turn → round transition
#### Round 3  (e=3/3 block=0 draw=8 disc=0)

  - hand:
    - [0] `DefendIronclad` cost=1 canPlay=True target=Self
    - [1] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [2] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [3] `BodySlam` cost=1 canPlay=True target=AnyEnemy
    - [4] `DefendIronclad` cost=1 canPlay=True target=Self
  - enemies:
    - [0] `Mawler` 53/72 block=0 → Debuff  powers=[VulnerablePower:1]
  - player powers: RagePower:3

  → play card [0]
  → play card [0] target=0
  → play card [0] target=0
  → end_turn → round transition
#### Round 4  (e=3/3 block=0 draw=3 disc=5)

  - hand:
    - [0] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [1] `Rage` cost=0 canPlay=True target=Self
    - [2] `Bloodletting` cost=0 canPlay=True target=Self
    - [3] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [4] `DefendIronclad` cost=1 canPlay=True target=Self
  - enemies:
    - [0] `Mawler` 35/72 block=0 → Attack 4×2
  - player powers: RagePower:3, VulnerablePower:3

  → play card [0] target=0
  → play card [0]
  → play card [0]
  → play card [0] target=0
  → play card [0]
  → end_turn → round transition
#### Round 5  (e=3/3 block=0 draw=8 disc=0)

  - hand:
    - [0] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [1] `DefendIronclad` cost=1 canPlay=True target=Self
    - [2] `Bash` cost=2 canPlay=True target=AnyEnemy
    - [3] `DefendIronclad` cost=1 canPlay=True target=Self
    - [4] `Rage` cost=0 canPlay=True target=Self
  - enemies:
    - [0] `Mawler` 20/72 block=0 → Attack 14×1
  - player powers: RagePower:6, VulnerablePower:3

  → play card [0] target=0
  → play card [0]
  → play card [1]
  → play card [1]
  → end_turn → round transition
#### Round 6  (e=3/3 block=0 draw=3 disc=5)

  - hand:
    - [0] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [1] `DefendIronclad` cost=1 canPlay=True target=Self
    - [2] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [3] `DefendIronclad` cost=1 canPlay=True target=Self
    - [4] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `Mawler` 14/72 block=0 → Attack 4×2
  - player powers: RagePower:9, VulnerablePower:3

  → play card [0] target=0
  → play card [0]
  → play card [0] target=0
  → end_turn → round transition
#### Round 7  (e=3/3 block=0 draw=8 disc=0)

  - hand:
    - [0] `BodySlam` cost=1 canPlay=True target=AnyEnemy
    - [1] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [2] `Bloodletting` cost=0 canPlay=True target=Self
    - [3] `DefendIronclad` cost=1 canPlay=True target=Self
    - [4] `DefendIronclad` cost=1 canPlay=True target=Self
  - enemies:
    - [0] `Mawler` 2/72 block=0 → Attack 14×1
  - player powers: RagePower:9, VulnerablePower:3

  → play card [0] target=0
  → play card [0] target=0
  - rewards offered:
    - [0] gold 10g  canSkip=False
    - [1] card cards=[`EvilEye`(cost=1), `Rage`(cost=0), `Cinder`(cost=2)]  canSkip=True

  - rewards offered:
    - [0] gold 10g  canSkip=False
    - [1] card cards=[`EvilEye`(cost=1), `Rage`(cost=0), `Cinder`(cost=2)]  canSkip=True

  → claim reward [0] → hp=57 room=CombatRoom
  - rewards offered:
    - [0] card cards=[`EvilEye`(cost=1), `Rage`(cost=0), `Cinder`(cost=2)]  canSkip=True

  - rewards offered:
    - [0] card cards=[`EvilEye`(cost=1), `Rage`(cost=0), `Cinder`(cost=2)]  canSkip=True

## Floor 9: MapRoom  (hp=57)

  - map options: (0,9):Treasure

  → skip reward [0] → hp=57 room=MapRoom
  - combat ended (hp=57)

  - heal → hp=80/80

## Floor 10: TreasureRoom  (hp=80)


  → pick map (0,9) → TreasureRoom floor=10
## Floor 10: MapRoom  (hp=80)

  - map options: (0,10):RestSite

  → take treasure → MapRoom relics=`BurningBlood`, `ScrollBoxes`, `OldCoin`, `Gorget`
## Floor 11: RestSiteRoom  (hp=80)


  → pick map (0,10) → RestSiteRoom floor=11
## Floor 11: MapRoom  (hp=80)

  - map options: (0,11):Unknown

  → pick rest option [1] → MapRoom hp=80
## Floor 12: CombatRoom  (hp=80)


  → pick map (0,11) → CombatRoom floor=12
### Combat #1 on floor 12

#### Round 1  (e=3/3 block=0 draw=8 disc=0)

  - hand:
    - [0] `Bloodletting` cost=0 canPlay=True target=Self
    - [1] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [2] `BodySlam` cost=1 canPlay=True target=AnyEnemy
    - [3] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [4] `DefendIronclad` cost=1 canPlay=True target=Self
  - enemies:
    - [0] `SnappingJaxfruit` 31/31 block=0 → Attack 3×1 + Buff
    - [1] `SlitheringStrangler` 54/54 block=0 → Debuff

  → play card [0]
  → play card [0] target=0
  → play card [0] target=0
  → play card [0] target=0
  → play card [0]
  → end_turn → round transition
#### Round 2  (e=3/3 block=0 draw=3 disc=5)

  - hand:
    - [0] `DefendIronclad` cost=1 canPlay=True target=Self
    - [1] `Rage` cost=0 canPlay=True target=Self
    - [2] `Bash` cost=2 canPlay=True target=AnyEnemy
    - [3] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [4] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `SnappingJaxfruit` 16/31 block=0 → Attack 3×1 + Buff  powers=[StrengthPower:2]
    - [1] `SlitheringStrangler` 54/54 block=0 → Attack 7×1 + Defend
  - player powers: ConstrictPower:3

  → play card [0]
  → play card [0]
  → play card [0] target=0
  → end_turn → round transition
#### Round 3  (e=3/3 block=0 draw=8 disc=0)

  - hand:
    - [0] `DefendIronclad` cost=1 canPlay=True target=Self
    - [1] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [2] `DefendIronclad` cost=1 canPlay=True target=Self
    - [3] `Bloodletting` cost=0 canPlay=True target=Self
    - [4] `DefendIronclad` cost=1 canPlay=True target=Self
  - enemies:
    - [0] `SnappingJaxfruit` 8/31 block=0 → Attack 3×1 + Buff  powers=[StrengthPower:4,VulnerablePower:1]
    - [1] `SlitheringStrangler` 54/54 block=5 → Debuff
  - player powers: ConstrictPower:3, RagePower:3

  → play card [0]
  → play card [0] target=0
  → play card [0]
  → play card [0]
  → play card [0]
  → end_turn → round transition
#### Round 4  (e=3/3 block=0 draw=3 disc=5)

  - hand:
    - [0] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [1] `DefendIronclad` cost=1 canPlay=True target=Self
    - [2] `BodySlam` cost=1 canPlay=True target=AnyEnemy
    - [3] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [4] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `SlitheringStrangler` 54/54 block=0 → Attack 7×1 + Defend
  - player powers: ConstrictPower:6, RagePower:3

  → play card [0] target=0
  → play card [0]
  → play card [0] target=0
  → end_turn → round transition
#### Round 5  (e=3/3 block=0 draw=8 disc=0)

  - hand:
    - [0] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [1] `Rage` cost=0 canPlay=True target=Self
    - [2] `Bash` cost=2 canPlay=True target=AnyEnemy
    - [3] `BodySlam` cost=1 canPlay=True target=AnyEnemy
    - [4] `DefendIronclad` cost=1 canPlay=True target=Self
  - enemies:
    - [0] `SlitheringStrangler` 43/54 block=5 → Debuff
  - player powers: ConstrictPower:6, RagePower:3

  → play card [0] target=0
  → play card [0]
  → play card [0] target=0
  → end_turn → round transition
#### Round 6  (e=3/3 block=0 draw=3 disc=5)

  - hand:
    - [0] `DefendIronclad` cost=1 canPlay=True target=Self
    - [1] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [2] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [3] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [4] `Bloodletting` cost=0 canPlay=True target=Self
  - enemies:
    - [0] `SlitheringStrangler` 34/54 block=0 → Attack 7×1 + Defend  powers=[VulnerablePower:1]
  - player powers: ConstrictPower:9, RagePower:6

  → play card [0]
  → play card [0] target=0
  → play card [0] target=0
  → play card [1]
  → play card [0] target=0
  → end_turn → round transition
#### Round 7  (e=3/3 block=0 draw=8 disc=0)

  - hand:
    - [0] `DefendIronclad` cost=1 canPlay=True target=Self
    - [1] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [2] `DefendIronclad` cost=1 canPlay=True target=Self
    - [3] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [4] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `SlitheringStrangler` 3/54 block=5 → Debuff
  - player powers: ConstrictPower:9, RagePower:6

  → play card [0]
  → play card [0] target=0
  - rewards offered:
    - [0] gold 10g  canSkip=False
    - [1] card cards=[`Anger`(cost=0), `BodySlam`(cost=1), `Breakthrough`(cost=1)]  canSkip=True

  - rewards offered:
    - [0] gold 10g  canSkip=False
    - [1] card cards=[`Anger`(cost=0), `BodySlam`(cost=1), `Breakthrough`(cost=1)]  canSkip=True

  → claim reward [0] → hp=60 room=CombatRoom
  - rewards offered:
    - [0] card cards=[`Anger`(cost=0), `BodySlam`(cost=1), `Breakthrough`(cost=1)]  canSkip=True

  - rewards offered:
    - [0] card cards=[`Anger`(cost=0), `BodySlam`(cost=1), `Breakthrough`(cost=1)]  canSkip=True

## Floor 12: MapRoom  (hp=60)

  - map options: (0,12):RestSite

  → skip reward [0] → hp=60 room=MapRoom
  - combat ended (hp=60)

  - heal → hp=80/80

## Floor 13: RestSiteRoom  (hp=80)


  → pick map (0,12) → RestSiteRoom floor=13
## Floor 13: MapRoom  (hp=80)

  - map options: (0,13):Unknown

  → pick rest option [1] → MapRoom hp=80
## Floor 14: EventRoom  (hp=80)

  - event options:
    - [0] `DENSE_VEGETATION.pages.INITIAL.options.TRUDGE_ON`
    - [1] `DENSE_VEGETATION.pages.INITIAL.options.REST`

  → pick map (0,13) → EventRoom floor=14
## Floor 14: MapRoom  (hp=80)

  - map options: (0,14):Monster

  → pick event option [1] → MapRoom hp=80
## Floor 15: CombatRoom  (hp=80)


  → pick map (0,14) → CombatRoom floor=15
### Combat #1 on floor 15

#### Round 1  (e=3/3 block=0 draw=8 disc=0)

  - hand:
    - [0] `Rage` cost=0 canPlay=True target=Self
    - [1] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [2] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [3] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [4] `Bash` cost=2 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `Fogmog` 74/74 block=0 → Unknown

  → play card [0]
  → play card [0] target=0
  → play card [0] target=0
  → play card [0] target=0
  → end_turn → round transition
#### Round 2  (e=3/3 block=0 draw=3 disc=5)

  - hand:
    - [0] `DefendIronclad` cost=1 canPlay=True target=Self
    - [1] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [2] `DefendIronclad` cost=1 canPlay=True target=Self
    - [3] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [4] `Bloodletting` cost=0 canPlay=True target=Self
  - enemies:
    - [0] `EyeWithTeeth` 6/6 block=0 → Unknown  powers=[IllusionPower:1,MinionPower:1]
    - [1] `Fogmog` 53/74 block=0 → Attack 8×1 + Buff
  - player powers: RagePower:3

  → play card [0]
  → play card [0] target=0
  → play card [0]
  → play card [1]
  → play card [0] target=0
  → end_turn → round transition
#### Round 3  (e=3/3 block=0 draw=8 disc=0)

  - hand:
    - [0] `BodySlam` cost=1 canPlay=True target=AnyEnemy
    - [1] `DefendIronclad` cost=1 canPlay=True target=Self
    - [2] `DefendIronclad` cost=1 canPlay=True target=Self
    - [3] `DefendIronclad` cost=1 canPlay=True target=Self
    - [4] `DefendIronclad` cost=1 canPlay=True target=Self
  - enemies:
    - [0] `EyeWithTeeth` 6/6 block=0 → Unknown  powers=[IllusionPower:1,MinionPower:1]
    - [1] `Fogmog` 44/74 block=0 → Attack 14×1  powers=[StrengthPower:1]
  - player powers: RagePower:3

  → play card [0] target=0
  → play card [0]
  → play card [0]
  → end_turn → round transition
#### Round 4  (e=3/3 block=0 draw=3 disc=8)

  - hand:
    - [0] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [1] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [2] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [3] `Bash` cost=2 canPlay=True target=AnyEnemy
    - [4] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `EyeWithTeeth` 6/6 block=0 → Unknown  powers=[IllusionPower:1,MinionPower:1]
    - [1] `Fogmog` 44/74 block=0 → Attack 8×1 + Buff  powers=[StrengthPower:1]
  - player powers: RagePower:3

  → play card [0] target=0
  → play card [0] target=0
  → play card [0] target=0
  → end_turn → round transition
#### Round 5  (e=3/3 block=0 draw=11 disc=0)

  - hand:
    - [0] `Bloodletting` cost=0 canPlay=True target=Self
    - [1] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [2] `Rage` cost=0 canPlay=True target=Self
    - [3] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [4] `Bash` cost=2 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `EyeWithTeeth` 6/6 block=0 → Unknown  powers=[IllusionPower:1,MinionPower:1]
    - [1] `Fogmog` 26/74 block=0 → Attack 14×1  powers=[StrengthPower:2]
  - player powers: RagePower:3

  → play card [0]
  → play card [0] target=0
  → play card [0]
  → play card [0] target=0
  → play card [0] target=0
  → end_turn → round transition
#### Round 6  (e=3/3 block=0 draw=6 disc=5)

  - hand:
    - [0] `Dazed` cost=0 canPlay=False target=None
    - [1] `DefendIronclad` cost=1 canPlay=True target=Self
    - [2] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [3] `BodySlam` cost=1 canPlay=True target=AnyEnemy
    - [4] `Dazed` cost=0 canPlay=False target=None
  - enemies:
    - [0] `EyeWithTeeth` 6/6 block=0 → Unknown  powers=[IllusionPower:1,MinionPower:1]
    - [1] `Fogmog` 9/74 block=0 → Attack 8×1 + Buff  powers=[StrengthPower:2,VulnerablePower:1]
  - player powers: RagePower:6

  → play card [1]
  → play card [1] target=0
  → play card [1] target=0
  → end_turn → round transition
#### Round 7  (e=3/3 block=0 draw=1 disc=8)

  - hand:
    - [0] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [1] `DefendIronclad` cost=1 canPlay=True target=Self
    - [2] `DefendIronclad` cost=1 canPlay=True target=Self
    - [3] `DefendIronclad` cost=1 canPlay=True target=Self
    - [4] `Dazed` cost=0 canPlay=False target=None
  - enemies:
    - [0] `EyeWithTeeth` 6/6 block=0 → Unknown  powers=[IllusionPower:1,MinionPower:1]
    - [1] `Fogmog` 2/74 block=0 → Attack 8×1 + Buff  powers=[StrengthPower:3]
  - player powers: RagePower:6

  → play card [0] target=0
  → play card [0]
  → play card [0]
  → end_turn → round transition
#### Round 8  (e=3/3 block=0 draw=8 disc=0)

  - hand:
    - [0] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [1] `DefendIronclad` cost=1 canPlay=True target=Self
    - [2] `DefendIronclad` cost=1 canPlay=True target=Self
    - [3] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [4] `DefendIronclad` cost=1 canPlay=True target=Self
  - enemies:
    - [0] `EyeWithTeeth` 6/6 block=0 → Unknown  powers=[IllusionPower:1,MinionPower:1]
    - [1] `Fogmog` 2/74 block=0 → Attack 14×1  powers=[StrengthPower:4]
  - player powers: RagePower:6

  → play card [0] target=0
  → play card [0]
  → play card [0]
  → end_turn → round transition
#### Round 9  (e=3/3 block=0 draw=3 disc=5)

  - hand:
    - [0] `Bash` cost=2 canPlay=True target=AnyEnemy
    - [1] `BodySlam` cost=1 canPlay=True target=AnyEnemy
    - [2] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [3] `Rage` cost=0 canPlay=True target=Self
    - [4] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `EyeWithTeeth` 6/6 block=0 → Unknown  powers=[IllusionPower:1,MinionPower:1]
    - [1] `Fogmog` 2/74 block=0 → Attack 8×1 + Buff  powers=[StrengthPower:4]
  - player powers: RagePower:6

  → play card [0] target=0
  → play card [0] target=0
  → play card [1]
  → end_turn → round transition
#### Round 10  (e=3/3 block=0 draw=8 disc=0)

  - hand:
    - [0] `DefendIronclad` cost=1 canPlay=True target=Self
    - [1] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [2] `Bloodletting` cost=0 canPlay=True target=Self
    - [3] `DefendIronclad` cost=1 canPlay=True target=Self
    - [4] `DefendIronclad` cost=1 canPlay=True target=Self
  - enemies:
    - [0] `EyeWithTeeth` 6/6 block=0 → Unknown  powers=[IllusionPower:1,MinionPower:1]
    - [1] `Fogmog` 2/74 block=0 → Attack 14×1  powers=[StrengthPower:5]
  - player powers: RagePower:9

  → play card [0]
  → play card [0] target=0
  → play card [0]
  → play card [0]
  → play card [0]
  → end_turn → round transition
#### Round 11  (e=3/3 block=0 draw=3 disc=5)

  - hand:
    - [0] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [1] `BodySlam` cost=1 canPlay=True target=AnyEnemy
    - [2] `Rage` cost=0 canPlay=True target=Self
    - [3] `DefendIronclad` cost=1 canPlay=True target=Self
    - [4] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `EyeWithTeeth` 6/6 block=0 → Unknown  powers=[IllusionPower:1,MinionPower:1]
    - [1] `Fogmog` 2/74 block=0 → Attack 8×1 + Buff  powers=[StrengthPower:5]
  - player powers: RagePower:9

  → play card [0] target=0
  → play card [0] target=0
  → play card [0]
  → play card [0]
  → end_turn → round transition
#### Round 12  (e=3/3 block=0 draw=8 disc=0)

  - hand:
    - [0] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [1] `Bash` cost=2 canPlay=True target=AnyEnemy
    - [2] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [3] `DefendIronclad` cost=1 canPlay=True target=Self
    - [4] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `EyeWithTeeth` 6/6 block=0 → Unknown  powers=[IllusionPower:1,MinionPower:1]
    - [1] `Fogmog` 2/74 block=0 → Attack 14×1  powers=[StrengthPower:6]
  - player powers: RagePower:12

  → play card [0] target=0
  → play card [0] target=0
  - rewards offered:
    - [0] gold 15g  canSkip=False
    - [1] potion potion=`AttackPotion`  canSkip=False
    - [2] card cards=[`Stampede`(cost=2), `BodySlam`(cost=1), `Bloodletting`(cost=0)]  canSkip=True

  - rewards offered:
    - [0] gold 15g  canSkip=False
    - [1] potion potion=`AttackPotion`  canSkip=False
    - [2] card cards=[`Stampede`(cost=2), `BodySlam`(cost=1), `Bloodletting`(cost=0)]  canSkip=True

  → claim reward [0] → hp=3 room=CombatRoom
  - rewards offered:
    - [0] potion potion=`AttackPotion`  canSkip=False
    - [1] card cards=[`Stampede`(cost=2), `BodySlam`(cost=1), `Bloodletting`(cost=0)]  canSkip=True

  - rewards offered:
    - [0] potion potion=`AttackPotion`  canSkip=False
    - [1] card cards=[`Stampede`(cost=2), `BodySlam`(cost=1), `Bloodletting`(cost=0)]  canSkip=True

  → claim reward [0] → hp=3 room=CombatRoom
  - rewards offered:
    - [0] card cards=[`Stampede`(cost=2), `BodySlam`(cost=1), `Bloodletting`(cost=0)]  canSkip=True

  - rewards offered:
    - [0] card cards=[`Stampede`(cost=2), `BodySlam`(cost=1), `Bloodletting`(cost=0)]  canSkip=True

## Floor 15: MapRoom  (hp=3)

  - map options: (0,15):RestSite

  → skip reward [0] → hp=3 room=MapRoom
  - combat ended (hp=3)

  - heal → hp=80/80

## Floor 16: RestSiteRoom  (hp=80)


  → pick map (0,15) → RestSiteRoom floor=16
## Floor 16: MapRoom  (hp=80)

  - map options: (3,16):Boss

  → pick rest option [1] → MapRoom hp=80
## Floor 17: BossRoom  (hp=80)


  → pick map (3,16) → BossRoom floor=17
### Combat #1 on floor 17

#### Round 1  (e=3/3 block=0 draw=8 disc=0)

  - hand:
    - [0] `Bash` cost=2 canPlay=True target=AnyEnemy
    - [1] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [2] `DefendIronclad` cost=1 canPlay=True target=Self
    - [3] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [4] `DefendIronclad` cost=1 canPlay=True target=Self
  - enemies:
    - [0] `Vantom` 173/173 block=0 → Attack 7×1  powers=[SlipperyPower:9]

  - heal → hp=80/80

  → play card [0] target=0
  → play card [0] target=0
  → end_turn → round transition
#### Round 2  (e=3/3 block=0 draw=3 disc=5)

  - hand:
    - [0] `Bloodletting` cost=0 canPlay=True target=Self
    - [1] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [2] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [3] `Rage` cost=0 canPlay=True target=Self
    - [4] `DefendIronclad` cost=1 canPlay=True target=Self
  - enemies:
    - [0] `Vantom` 171/173 block=0 → Attack 6×2  powers=[SlipperyPower:7,VulnerablePower:1]

  → play card [0]
  → play card [0] target=0
  → play card [0] target=0
  → play card [0]
  → play card [0]
  → end_turn → round transition
#### Round 3  (e=3/3 block=0 draw=8 disc=0)

  - hand:
    - [0] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [1] `BodySlam` cost=1 canPlay=True target=AnyEnemy
    - [2] `DefendIronclad` cost=1 canPlay=True target=Self
    - [3] `DefendIronclad` cost=1 canPlay=True target=Self
    - [4] `Bloodletting` cost=0 canPlay=True target=Self
  - enemies:
    - [0] `Vantom` 169/173 block=0 → Attack 27×1 + Unknown  powers=[SlipperyPower:5]
  - player powers: RagePower:3

  → play card [0] target=0
  → play card [0] target=0
  → play card [0]
  → play card [1]
  → play card [0]
  → end_turn → round transition
#### Round 4  (e=3/3 block=0 draw=3 disc=8)

  - hand:
    - [0] `Rage` cost=0 canPlay=True target=Self
    - [1] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [2] `DefendIronclad` cost=1 canPlay=True target=Self
    - [3] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [4] `Bash` cost=2 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `Vantom` 168/173 block=0 → Buff  powers=[SlipperyPower:4]
  - player powers: RagePower:3

  → play card [0]
  → play card [0] target=0
  → play card [0]
  → play card [0] target=0
  → end_turn → round transition
#### Round 5  (e=3/3 block=0 draw=11 disc=0)

  - hand:
    - [0] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [1] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [2] `DefendIronclad` cost=1 canPlay=True target=Self
    - [3] `Wound` cost=0 canPlay=False target=None
    - [4] `DefendIronclad` cost=1 canPlay=True target=Self
  - enemies:
    - [0] `Vantom` 166/173 block=0 → Attack 7×1  powers=[SlipperyPower:2,StrengthPower:2]
  - player powers: RagePower:6

  → play card [0] target=0
  → play card [0] target=0
  → play card [0]
  → end_turn → round transition
#### Round 6  (e=3/3 block=0 draw=6 disc=5)

  - hand:
    - [0] `Wound` cost=0 canPlay=False target=None
    - [1] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [2] `DefendIronclad` cost=1 canPlay=True target=Self
    - [3] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [4] `Bloodletting` cost=0 canPlay=True target=Self
  - enemies:
    - [0] `Vantom` 164/173 block=0 → Attack 6×2  powers=[StrengthPower:2]
  - player powers: RagePower:6

  → play card [1] target=0
  → play card [1]
  → play card [1] target=0
  → play card [1]
  → end_turn → round transition
#### Round 7  (e=3/3 block=0 draw=1 disc=10)

  - hand:
    - [0] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [1] `BodySlam` cost=1 canPlay=True target=AnyEnemy
    - [2] `DefendIronclad` cost=1 canPlay=True target=Self
    - [3] `Rage` cost=0 canPlay=True target=Self
    - [4] `Bash` cost=2 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `Vantom` 149/173 block=0 → Attack 27×1 + Unknown  powers=[StrengthPower:2]
  - player powers: RagePower:6

  → play card [0] target=0
  → play card [0] target=0
  → play card [0]
  → play card [0]
  → end_turn → round transition
#### Round 8  (e=3/3 block=0 draw=14 disc=0)

  - hand:
    - [0] `Wound` cost=0 canPlay=False target=None
    - [1] `DefendIronclad` cost=1 canPlay=True target=Self
    - [2] `Wound` cost=0 canPlay=False target=None
    - [3] `Wound` cost=0 canPlay=False target=None
    - [4] `Bloodletting` cost=0 canPlay=True target=Self
  - enemies:
    - [0] `Vantom` 140/173 block=0 → Buff  powers=[StrengthPower:2]
  - player powers: RagePower:9

  → play card [1]
  → play card [3]
  - rewards offered:
    - [0] gold 100g  canSkip=False
    - [1] potion potion=`FlexPotion`  canSkip=False
    - [2] card cards=[`CrimsonMantle`(cost=1), `Barricade`(cost=3), `Cruelty`(cost=1)]  canSkip=True

  - rewards offered:
    - [0] gold 100g  canSkip=False
    - [1] potion potion=`FlexPotion`  canSkip=False
    - [2] card cards=[`CrimsonMantle`(cost=1), `Barricade`(cost=3), `Cruelty`(cost=1)]  canSkip=True

  - heal → hp=80/80

  - rewards offered:
    - [0] gold 100g  canSkip=False
    - [1] potion potion=`FlexPotion`  canSkip=False
    - [2] card cards=[`CrimsonMantle`(cost=1), `Barricade`(cost=3), `Cruelty`(cost=1)]  canSkip=True

  → claim reward [0] → hp=80 room=BossRoom
  - rewards offered:
    - [0] potion potion=`FlexPotion`  canSkip=False
    - [1] card cards=[`CrimsonMantle`(cost=1), `Barricade`(cost=3), `Cruelty`(cost=1)]  canSkip=True

  - rewards offered:
    - [0] potion potion=`FlexPotion`  canSkip=False
    - [1] card cards=[`CrimsonMantle`(cost=1), `Barricade`(cost=3), `Cruelty`(cost=1)]  canSkip=True

  → claim reward [0] → hp=80 room=BossRoom
  - rewards offered:
    - [0] card cards=[`CrimsonMantle`(cost=1), `Barricade`(cost=3), `Cruelty`(cost=1)]  canSkip=True

  - rewards offered:
    - [0] card cards=[`CrimsonMantle`(cost=1), `Barricade`(cost=3), `Cruelty`(cost=1)]  canSkip=True

## Floor 17: MapRoom  (hp=80)


  → skip reward [0] → hp=80 room=MapRoom
  - combat ended (hp=80)

## Floor 0: MapRoom  (hp=80)

  - map options: (3,0):Unknown, (1,1):Monster, (3,1):Monster

  → enter_next_act → MapRoom floor=0
## Floor 2: CombatRoom  (hp=80)


  → pick map (1,1) → CombatRoom floor=2
### Combat #1 on floor 2

#### Round 1  (e=3/3 block=0 draw=8 disc=0)

  - hand:
    - [0] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [1] `DefendIronclad` cost=1 canPlay=True target=Self
    - [2] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [3] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [4] `Rage` cost=0 canPlay=True target=Self
  - enemies:
    - [0] `BowlbugRock` 47/47 block=0 → Attack 15×1  powers=[ImbalancedPower:1]
    - [1] `BowlbugNectar` 36/36 block=0 → Attack 3×1

  → play card [0] target=0
  → play card [0]
  → play card [0] target=0
  → play card [1]
  → end_turn → round transition
#### Round 2  (e=3/3 block=0 draw=3 disc=5)

  - hand:
    - [0] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [1] `BodySlam` cost=1 canPlay=True target=AnyEnemy
    - [2] `Bloodletting` cost=0 canPlay=True target=Self
    - [3] `DefendIronclad` cost=1 canPlay=True target=Self
    - [4] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `BowlbugRock` 29/47 block=0 → Attack 15×1  powers=[ImbalancedPower:1]
    - [1] `BowlbugNectar` 36/36 block=0 → Buff
  - player powers: RagePower:3

  → play card [0] target=0
  → play card [0] target=0
  → play card [0]
  → play card [0]
  → play card [0] target=0
  → end_turn → round transition
#### Round 3  (e=3/3 block=0 draw=8 disc=0)

  - hand:
    - [0] `DefendIronclad` cost=1 canPlay=True target=Self
    - [1] `DefendIronclad` cost=1 canPlay=True target=Self
    - [2] `Bash` cost=2 canPlay=True target=AnyEnemy
    - [3] `BodySlam` cost=1 canPlay=True target=AnyEnemy
    - [4] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `BowlbugRock` 14/47 block=0 → Attack 15×1  powers=[ImbalancedPower:1]
    - [1] `BowlbugNectar` 36/36 block=0 → Attack 3×1  powers=[StrengthPower:15]
  - player powers: RagePower:3

  → play card [0]
  → play card [0]
  → play card [1] target=0
  → end_turn → round transition
#### Round 4  (e=3/3 block=0 draw=3 disc=5)

  - hand:
    - [0] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [1] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [2] `Bloodletting` cost=0 canPlay=True target=Self
    - [3] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [4] `Rage` cost=0 canPlay=True target=Self
  - enemies:
    - [0] `BowlbugRock` 4/47 block=0 → Attack 15×1  powers=[ImbalancedPower:1]
    - [1] `BowlbugNectar` 36/36 block=0 → Attack 3×1  powers=[StrengthPower:15]
  - player powers: RagePower:3

  → play card [0] target=0
  → play card [0] target=0
  → play card [0]
  → play card [0] target=0
  → play card [0]
  → end_turn → round transition
#### Round 5  (e=3/3 block=0 draw=8 disc=0)

  - hand:
    - [0] `DefendIronclad` cost=1 canPlay=True target=Self
    - [1] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [2] `DefendIronclad` cost=1 canPlay=True target=Self
    - [3] `Bash` cost=2 canPlay=True target=AnyEnemy
    - [4] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `BowlbugNectar` 18/36 block=0 → Attack 3×1  powers=[StrengthPower:15]
  - player powers: RagePower:6

  → play card [0]
  → play card [0] target=0
  → play card [0]
  → end_turn → round transition
#### Round 6  (e=3/3 block=0 draw=3 disc=5)

  - hand:
    - [0] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [1] `DefendIronclad` cost=1 canPlay=True target=Self
    - [2] `DefendIronclad` cost=1 canPlay=True target=Self
    - [3] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [4] `Rage` cost=0 canPlay=True target=Self
  - enemies:
    - [0] `BowlbugNectar` 12/36 block=0 → Attack 3×1  powers=[StrengthPower:15]
  - player powers: RagePower:6

  → play card [0] target=0
  → play card [0]
  → play card [0]
  → play card [1]
  → end_turn → round transition
  - rewards offered:
    - [0] gold 17g  canSkip=False
    - [1] card cards=[`Inferno`(cost=1), `SwordBoomerang`(cost=1), `ShrugItOff`(cost=1)]  canSkip=True

  - rewards offered:
    - [0] gold 17g  canSkip=False
    - [1] card cards=[`Inferno`(cost=1), `SwordBoomerang`(cost=1), `ShrugItOff`(cost=1)]  canSkip=True

  - heal → hp=80/80

  - rewards offered:
    - [0] gold 17g  canSkip=False
    - [1] card cards=[`Inferno`(cost=1), `SwordBoomerang`(cost=1), `ShrugItOff`(cost=1)]  canSkip=True

  → claim reward [0] → hp=80 room=CombatRoom
  - rewards offered:
    - [0] card cards=[`Inferno`(cost=1), `SwordBoomerang`(cost=1), `ShrugItOff`(cost=1)]  canSkip=True

  - rewards offered:
    - [0] card cards=[`Inferno`(cost=1), `SwordBoomerang`(cost=1), `ShrugItOff`(cost=1)]  canSkip=True

## Floor 2: MapRoom  (hp=80)

  - map options: (0,2):Monster

  → skip reward [0] → hp=80 room=MapRoom
  - combat ended (hp=80)

## Floor 3: CombatRoom  (hp=80)


  → pick map (0,2) → CombatRoom floor=3
### Combat #1 on floor 3

#### Round 1  (e=3/3 block=0 draw=8 disc=0)

  - hand:
    - [0] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [1] `DefendIronclad` cost=1 canPlay=True target=Self
    - [2] `BodySlam` cost=1 canPlay=True target=AnyEnemy
    - [3] `DefendIronclad` cost=1 canPlay=True target=Self
    - [4] `DefendIronclad` cost=1 canPlay=True target=Self
  - enemies:
    - [0] `ThievingHopper` 79/79 block=0 → Attack 17×1 + Unknown  powers=[EscapeArtistPower:5]

  → play card [0] target=0
  → play card [0]
  → play card [0] target=0
  → end_turn → round transition
#### Round 2  (e=3/3 block=0 draw=2 disc=5)

  - hand:
    - [0] `DefendIronclad` cost=1 canPlay=True target=Self
    - [1] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [2] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [3] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [4] `Bloodletting` cost=0 canPlay=True target=Self
  - enemies:
    - [0] `ThievingHopper` 68/79 block=0 → Buff  powers=[EscapeArtistPower:5,SwipePower:1]

  → play card [0]
  → play card [0] target=0
  → play card [0] target=0
  → play card [1]
  → play card [0] target=0
  → end_turn → round transition
#### Round 3  (e=3/3 block=0 draw=7 disc=0)

  - hand:
    - [0] `Bash` cost=2 canPlay=True target=AnyEnemy
    - [1] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [2] `DefendIronclad` cost=1 canPlay=True target=Self
    - [3] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [4] `Bloodletting` cost=0 canPlay=True target=Self
  - enemies:
    - [0] `ThievingHopper` 41/79 block=0 → Attack 21×1  powers=[EscapeArtistPower:5,SwipePower:1,FlutterPower:5]

  → play card [0] target=0
  → play card [0] target=0
  → play card [2]
  → play card [0]
  → play card [0] target=0
  → end_turn → round transition
#### Round 4  (e=3/3 block=0 draw=2 disc=5)

  - hand:
    - [0] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [1] `BodySlam` cost=1 canPlay=True target=AnyEnemy
    - [2] `DefendIronclad` cost=1 canPlay=True target=Self
    - [3] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [4] `DefendIronclad` cost=1 canPlay=True target=Self
  - enemies:
    - [0] `ThievingHopper` 25/79 block=0 → Attack 14×1  powers=[EscapeArtistPower:5,SwipePower:1,FlutterPower:2,VulnerablePower:1]

  → play card [0] target=0
  → play card [0] target=0
  → play card [0]
  → end_turn → round transition
#### Round 5  (e=3/3 block=0 draw=7 disc=0)

  - hand:
    - [0] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [1] `DefendIronclad` cost=1 canPlay=True target=Self
    - [2] `Bash` cost=2 canPlay=True target=AnyEnemy
    - [3] `DefendIronclad` cost=1 canPlay=True target=Self
    - [4] `DefendIronclad` cost=1 canPlay=True target=Self
  - enemies:
    - [0] `ThievingHopper` 19/79 block=0 → Escape  powers=[EscapeArtistPower:5,SwipePower:1,FlutterPower:1]

  → play card [0] target=0
  → play card [0]
  → play card [1]
  → end_turn → round transition
#### Round 6  (e=3/3 block=0 draw=2 disc=5)

  - hand:
    - [0] `DefendIronclad` cost=1 canPlay=True target=Self
    - [1] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [2] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [3] `Bloodletting` cost=0 canPlay=True target=Self
    - [4] `BodySlam` cost=1 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `ThievingHopper` 15/79 block=0 → Escape  powers=[EscapeArtistPower:5,SwipePower:1]

  → play card [0]
  → play card [0] target=0
  → play card [0] target=0
  - rewards offered:
    - [0] gold 14g  canSkip=False
    - [1] potion potion=`CureAll`  canSkip=False
    - [2] unknown  canSkip=False
    - [3] card cards=[`MoltenFist`(cost=1), `StoneArmor`(cost=1), `Breakthrough`(cost=1)]  canSkip=True

  - rewards offered:
    - [0] gold 14g  canSkip=False
    - [1] potion potion=`CureAll`  canSkip=False
    - [2] unknown  canSkip=False
    - [3] card cards=[`MoltenFist`(cost=1), `StoneArmor`(cost=1), `Breakthrough`(cost=1)]  canSkip=True

  → claim reward [0] → hp=37 room=CombatRoom
  - rewards offered:
    - [0] potion potion=`CureAll`  canSkip=False
    - [1] unknown  canSkip=False
    - [2] card cards=[`MoltenFist`(cost=1), `StoneArmor`(cost=1), `Breakthrough`(cost=1)]  canSkip=True

  - rewards offered:
    - [0] potion potion=`CureAll`  canSkip=False
    - [1] unknown  canSkip=False
    - [2] card cards=[`MoltenFist`(cost=1), `StoneArmor`(cost=1), `Breakthrough`(cost=1)]  canSkip=True

  → claim reward [0] → hp=37 room=CombatRoom
  - rewards offered:
    - [0] unknown  canSkip=False
    - [1] card cards=[`MoltenFist`(cost=1), `StoneArmor`(cost=1), `Breakthrough`(cost=1)]  canSkip=True

  - rewards offered:
    - [0] unknown  canSkip=False
    - [1] card cards=[`MoltenFist`(cost=1), `StoneArmor`(cost=1), `Breakthrough`(cost=1)]  canSkip=True

  → claim reward [0] → hp=37 room=CombatRoom
  - rewards offered:
    - [0] card cards=[`MoltenFist`(cost=1), `StoneArmor`(cost=1), `Breakthrough`(cost=1)]  canSkip=True

  - rewards offered:
    - [0] card cards=[`MoltenFist`(cost=1), `StoneArmor`(cost=1), `Breakthrough`(cost=1)]  canSkip=True

## Floor 3: MapRoom  (hp=37)

  - map options: (0,3):Monster

  → skip reward [0] → hp=37 room=MapRoom
  - combat ended (hp=37)

  - heal → hp=80/80

## Floor 4: CombatRoom  (hp=80)


  → pick map (0,3) → CombatRoom floor=4
### Combat #1 on floor 4

#### Round 1  (e=3/3 block=0 draw=8 disc=0)

  - hand:
    - [0] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [1] `Bash` cost=2 canPlay=True target=AnyEnemy
    - [2] `DefendIronclad` cost=1 canPlay=True target=Self
    - [3] `DefendIronclad` cost=1 canPlay=True target=Self
    - [4] `DefendIronclad` cost=1 canPlay=True target=Self
  - enemies:
    - [0] `HunterKiller` 121/121 block=0 → Debuff

  → play card [0] target=0
  → play card [0] target=0
  → end_turn → round transition
#### Round 2  (e=3/3 block=0 draw=3 disc=5)

  - hand:
    - [0] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [1] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [2] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [3] `Bloodletting` cost=0 canPlay=True target=Self
    - [4] `DefendIronclad` cost=1 canPlay=True target=Self
  - enemies:
    - [0] `HunterKiller` 107/121 block=0 → Attack 17×1  powers=[VulnerablePower:1]
  - player powers: TenderPower:1

  → play card [0] target=0
  → play card [0] target=0
  → play card [0] target=0
  → play card [0]
  → play card [0]
  → end_turn → round transition
#### Round 3  (e=3/3 block=0 draw=8 disc=0)

  - hand:
    - [0] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [1] `BodySlam` cost=1 canPlay=True target=AnyEnemy
    - [2] `Rage` cost=0 canPlay=True target=Self
    - [3] `DefendIronclad` cost=1 canPlay=True target=Self
    - [4] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `HunterKiller` 68/121 block=0 → Attack 7×3
  - player powers: TenderPower:1

  → play card [0] target=0
  → play card [0] target=0
  → play card [0]
  → play card [0]
  → end_turn → round transition
#### Round 4  (e=3/3 block=0 draw=3 disc=5)

  - hand:
    - [0] `DefendIronclad` cost=1 canPlay=True target=Self
    - [1] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [2] `Bloodletting` cost=0 canPlay=True target=Self
    - [3] `Bash` cost=2 canPlay=True target=AnyEnemy
    - [4] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `HunterKiller` 59/121 block=0 → Attack 17×1
  - player powers: TenderPower:1, RagePower:3

  → play card [0]
  → play card [0] target=0
  → play card [0]
  → play card [0] target=0
  → play card [0] target=0
  → end_turn → round transition
#### Round 5  (e=3/3 block=0 draw=8 disc=0)

  - hand:
    - [0] `DefendIronclad` cost=1 canPlay=True target=Self
    - [1] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [2] `DefendIronclad` cost=1 canPlay=True target=Self
    - [3] `DefendIronclad` cost=1 canPlay=True target=Self
    - [4] `Bloodletting` cost=0 canPlay=True target=Self
  - enemies:
    - [0] `HunterKiller` 33/121 block=0 → Attack 7×3  powers=[VulnerablePower:1]
  - player powers: TenderPower:1, RagePower:3

  → play card [0]
  → play card [0] target=0
  → play card [0]
  → play card [1]
  → play card [0]
  → end_turn → round transition
#### Round 6  (e=3/3 block=0 draw=3 disc=5)

  - hand:
    - [0] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [1] `Rage` cost=0 canPlay=True target=Self
    - [2] `Bash` cost=2 canPlay=True target=AnyEnemy
    - [3] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [4] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `HunterKiller` 20/121 block=0 → Attack 7×3
  - player powers: TenderPower:1, RagePower:3

  → play card [0] target=0
  → play card [0]
  → play card [0] target=0
  → end_turn → round transition
#### Round 7  (e=3/3 block=0 draw=8 disc=0)

  - hand:
    - [0] `BodySlam` cost=1 canPlay=True target=AnyEnemy
    - [1] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [2] `DefendIronclad` cost=1 canPlay=True target=Self
    - [3] `DefendIronclad` cost=1 canPlay=True target=Self
    - [4] `DefendIronclad` cost=1 canPlay=True target=Self
  - enemies:
    - [0] `HunterKiller` 3/121 block=0 → Attack 17×1  powers=[VulnerablePower:1]
  - player powers: TenderPower:1, RagePower:6

  → play card [0] target=0
  → play card [0] target=0
  - rewards offered:
    - [0] gold 20g  canSkip=False
    - [1] card cards=[`SwordBoomerang`(cost=1), `Feed`(cost=1), `TrueGrit`(cost=1)]  canSkip=True

  - rewards offered:
    - [0] gold 20g  canSkip=False
    - [1] card cards=[`SwordBoomerang`(cost=1), `Feed`(cost=1), `TrueGrit`(cost=1)]  canSkip=True

  → claim reward [0] → hp=4 room=CombatRoom
  - rewards offered:
    - [0] card cards=[`SwordBoomerang`(cost=1), `Feed`(cost=1), `TrueGrit`(cost=1)]  canSkip=True

  - rewards offered:
    - [0] card cards=[`SwordBoomerang`(cost=1), `Feed`(cost=1), `TrueGrit`(cost=1)]  canSkip=True

## Floor 4: MapRoom  (hp=4)

  - map options: (0,4):Unknown

  → skip reward [0] → hp=4 room=MapRoom
  - combat ended (hp=4)

  - heal → hp=80/80

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

  → pick rest option [1] → MapRoom hp=80
## Floor 8: CombatRoom  (hp=80)


  → pick map (1,7) → CombatRoom floor=8
### Combat #1 on floor 8

#### Round 1  (e=3/3 block=0 draw=8 disc=0)

  - hand:
    - [0] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [1] `DefendIronclad` cost=1 canPlay=True target=Self
    - [2] `Bash` cost=2 canPlay=True target=AnyEnemy
    - [3] `DefendIronclad` cost=1 canPlay=True target=Self
    - [4] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `SpinyToad` 116/116 block=0 → Buff

  → play card [0] target=0
  → play card [0]
  → play card [1]
  → end_turn → round transition
#### Round 2  (e=3/3 block=0 draw=3 disc=5)

  - hand:
    - [0] `Rage` cost=0 canPlay=True target=Self
    - [1] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [2] `Bloodletting` cost=0 canPlay=True target=Self
    - [3] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [4] `DefendIronclad` cost=1 canPlay=True target=Self
  - enemies:
    - [0] `SpinyToad` 107/116 block=0 → Attack 23×1  powers=[ThornsPower:5]

  → play card [0]
  → play card [0] target=0
  → play card [0]
  → play card [0] target=0
  → play card [0]
  → end_turn → round transition
#### Round 3  (e=3/3 block=0 draw=8 disc=0)

  - hand:
    - [0] `BodySlam` cost=1 canPlay=True target=AnyEnemy
    - [1] `DefendIronclad` cost=1 canPlay=True target=Self
    - [2] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [3] `Rage` cost=0 canPlay=True target=Self
    - [4] `Bloodletting` cost=0 canPlay=True target=Self
  - enemies:
    - [0] `SpinyToad` 89/116 block=0 → Attack 17×1
  - player powers: RagePower:3

  → play card [0] target=0
  → play card [0]
  → play card [0] target=0
  → play card [0]
  → play card [0]
  → end_turn → round transition
#### Round 4  (e=3/3 block=0 draw=3 disc=5)

  - hand:
    - [0] `Bash` cost=2 canPlay=True target=AnyEnemy
    - [1] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [2] `DefendIronclad` cost=1 canPlay=True target=Self
    - [3] `DefendIronclad` cost=1 canPlay=True target=Self
    - [4] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `SpinyToad` 80/116 block=0 → Buff
  - player powers: RagePower:6

  → play card [0] target=0
  → play card [0] target=0
  → end_turn → round transition
#### Round 5  (e=3/3 block=0 draw=8 disc=0)

  - hand:
    - [0] `DefendIronclad` cost=1 canPlay=True target=Self
    - [1] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [2] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [3] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [4] `BodySlam` cost=1 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `SpinyToad` 59/116 block=0 → Attack 23×1  powers=[VulnerablePower:1,ThornsPower:5]
  - player powers: RagePower:6

  → play card [0]
  → play card [0] target=0
  → play card [0] target=0
  → end_turn → round transition
#### Round 6  (e=3/3 block=0 draw=3 disc=5)

  - hand:
    - [0] `DefendIronclad` cost=1 canPlay=True target=Self
    - [1] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [2] `DefendIronclad` cost=1 canPlay=True target=Self
    - [3] `Bash` cost=2 canPlay=True target=AnyEnemy
    - [4] `Rage` cost=0 canPlay=True target=Self
  - enemies:
    - [0] `SpinyToad` 33/116 block=0 → Attack 17×1
  - player powers: RagePower:6

  → play card [0]
  → play card [0] target=0
  → play card [0]
  → play card [1]
  → end_turn → round transition
  - rewards offered:
    - [0] gold 15g  canSkip=False
    - [1] card cards=[`Cinder`(cost=2), `FlameBarrier`(cost=2), `BodySlam`(cost=1)]  canSkip=True

  - rewards offered:
    - [0] gold 15g  canSkip=False
    - [1] card cards=[`Cinder`(cost=2), `FlameBarrier`(cost=2), `BodySlam`(cost=1)]  canSkip=True

  - heal → hp=80/80

  - rewards offered:
    - [0] gold 15g  canSkip=False
    - [1] card cards=[`Cinder`(cost=2), `FlameBarrier`(cost=2), `BodySlam`(cost=1)]  canSkip=True

  → claim reward [0] → hp=80 room=CombatRoom
  - rewards offered:
    - [0] card cards=[`Cinder`(cost=2), `FlameBarrier`(cost=2), `BodySlam`(cost=1)]  canSkip=True

  - rewards offered:
    - [0] card cards=[`Cinder`(cost=2), `FlameBarrier`(cost=2), `BodySlam`(cost=1)]  canSkip=True

## Floor 8: MapRoom  (hp=80)

  - map options: (2,8):Treasure

  → skip reward [0] → hp=80 room=MapRoom
  - combat ended (hp=80)

## Floor 9: TreasureRoom  (hp=80)


  → pick map (2,8) → TreasureRoom floor=9
## Floor 9: MapRoom  (hp=80)

  - map options: (2,9):Elite, (1,9):Monster, (3,9):Unknown

  → take treasure → MapRoom relics=`BurningBlood`, `ScrollBoxes`, `OldCoin`, `Gorget`, `JuzuBracelet`
## Floor 10: CombatRoom  (hp=80)


  → pick map (1,9) → CombatRoom floor=10
### Combat #1 on floor 10

#### Round 1  (e=3/3 block=0 draw=8 disc=0)

  - hand:
    - [0] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [1] `Bash` cost=2 canPlay=True target=AnyEnemy
    - [2] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [3] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [4] `BodySlam` cost=1 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `Exoskeleton` 27/27 block=0 → Attack 1×3  powers=[HardToKillPower:9]
    - [1] `Exoskeleton` 24/24 block=0 → Attack 8×1  powers=[HardToKillPower:9]
    - [2] `Exoskeleton` 25/25 block=0 → Buff  powers=[HardToKillPower:9]
    - [3] `Exoskeleton` 26/26 block=0 → Attack 1×3  powers=[HardToKillPower:9]

  → play card [0] target=0
  → play card [0] target=0
  → end_turn → round transition
#### Round 2  (e=3/3 block=0 draw=3 disc=5)

  - hand:
    - [0] `Rage` cost=0 canPlay=True target=Self
    - [1] `DefendIronclad` cost=1 canPlay=True target=Self
    - [2] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [3] `Bloodletting` cost=0 canPlay=True target=Self
    - [4] `DefendIronclad` cost=1 canPlay=True target=Self
  - enemies:
    - [0] `Exoskeleton` 10/27 block=0 → Attack 8×1  powers=[HardToKillPower:9,VulnerablePower:1]
    - [1] `Exoskeleton` 24/24 block=0 → Buff  powers=[HardToKillPower:9]
    - [2] `Exoskeleton` 25/25 block=0 → Attack 8×1  powers=[HardToKillPower:9,StrengthPower:2]
    - [3] `Exoskeleton` 26/26 block=0 → Attack 8×1  powers=[HardToKillPower:9]

  → play card [0]
  → play card [0]
  → play card [0] target=0
  → play card [0]
  → play card [0]
  → end_turn → round transition
#### Round 3  (e=3/3 block=0 draw=8 disc=0)

  - hand:
    - [0] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [1] `DefendIronclad` cost=1 canPlay=True target=Self
    - [2] `DefendIronclad` cost=1 canPlay=True target=Self
    - [3] `Bloodletting` cost=0 canPlay=True target=Self
    - [4] `DefendIronclad` cost=1 canPlay=True target=Self
  - enemies:
    - [0] `Exoskeleton` 1/27 block=0 → Buff  powers=[HardToKillPower:9]
    - [1] `Exoskeleton` 24/24 block=0 → Attack 8×1  powers=[HardToKillPower:9,StrengthPower:2]
    - [2] `Exoskeleton` 25/25 block=0 → Buff  powers=[HardToKillPower:9,StrengthPower:2]
    - [3] `Exoskeleton` 26/26 block=0 → Buff  powers=[HardToKillPower:9]
  - player powers: RagePower:3

  → play card [0] target=0
  → play card [0]
  → play card [0]
  → play card [0]
  → play card [0]
  → end_turn → round transition
#### Round 4  (e=3/3 block=0 draw=3 disc=5)

  - hand:
    - [0] `DefendIronclad` cost=1 canPlay=True target=Self
    - [1] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [2] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [3] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [4] `Bash` cost=2 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `Exoskeleton` 24/24 block=0 → Buff  powers=[HardToKillPower:9,StrengthPower:2]
    - [1] `Exoskeleton` 25/25 block=0 → Attack 1×3  powers=[HardToKillPower:9,StrengthPower:4]
    - [2] `Exoskeleton` 26/26 block=0 → Attack 1×3  powers=[HardToKillPower:9,StrengthPower:2]
  - player powers: RagePower:3

  → play card [0]
  → play card [0] target=0
  → play card [0] target=0
  → end_turn → round transition
#### Round 5  (e=3/3 block=0 draw=8 disc=0)

  - hand:
    - [0] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [1] `Rage` cost=0 canPlay=True target=Self
    - [2] `BodySlam` cost=1 canPlay=True target=AnyEnemy
    - [3] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [4] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `Exoskeleton` 6/24 block=0 → Attack 1×3  powers=[HardToKillPower:9,StrengthPower:4]
    - [1] `Exoskeleton` 25/25 block=0 → Attack 8×1  powers=[HardToKillPower:9,StrengthPower:4]
    - [2] `Exoskeleton` 26/26 block=0 → Attack 8×1  powers=[HardToKillPower:9,StrengthPower:2]
  - player powers: RagePower:3

  → play card [0] target=0
  → play card [0]
  → play card [0] target=0
  → play card [0] target=0
  → end_turn → round transition
#### Round 6  (e=3/3 block=0 draw=3 disc=5)

  - hand:
    - [0] `DefendIronclad` cost=1 canPlay=True target=Self
    - [1] `Bloodletting` cost=0 canPlay=True target=Self
    - [2] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [3] `DefendIronclad` cost=1 canPlay=True target=Self
    - [4] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `Exoskeleton` 16/25 block=0 → Buff  powers=[HardToKillPower:9,StrengthPower:4]
    - [1] `Exoskeleton` 26/26 block=0 → Buff  powers=[HardToKillPower:9,StrengthPower:2]
  - player powers: RagePower:6

  → play card [0]
  → play card [0]
  - rewards offered:
    - [0] gold 20g  canSkip=False
    - [1] card cards=[`Breakthrough`(cost=1), `PommelStrike`(cost=1), `Taunt`(cost=1)]  canSkip=True

  - rewards offered:
    - [0] gold 20g  canSkip=False
    - [1] card cards=[`Breakthrough`(cost=1), `PommelStrike`(cost=1), `Taunt`(cost=1)]  canSkip=True

  - heal → hp=80/80

  - rewards offered:
    - [0] gold 20g  canSkip=False
    - [1] card cards=[`Breakthrough`(cost=1), `PommelStrike`(cost=1), `Taunt`(cost=1)]  canSkip=True

  → claim reward [0] → hp=80 room=CombatRoom
  - rewards offered:
    - [0] card cards=[`Breakthrough`(cost=1), `PommelStrike`(cost=1), `Taunt`(cost=1)]  canSkip=True

  - rewards offered:
    - [0] card cards=[`Breakthrough`(cost=1), `PommelStrike`(cost=1), `Taunt`(cost=1)]  canSkip=True

## Floor 10: MapRoom  (hp=80)

  - map options: (0,10):Unknown, (1,10):RestSite

  → skip reward [0] → hp=80 room=MapRoom
  - combat ended (hp=80)

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

#### Round 1  (e=3/3 block=0 draw=8 disc=0)

  - hand:
    - [0] `DefendIronclad` cost=1 canPlay=True target=Self
    - [1] `DefendIronclad` cost=1 canPlay=True target=Self
    - [2] `Rage` cost=0 canPlay=True target=Self
    - [3] `BodySlam` cost=1 canPlay=True target=AnyEnemy
    - [4] `DefendIronclad` cost=1 canPlay=True target=Self
  - enemies:
    - [0] `BowlbugRock` 46/46 block=0 → Attack 15×1  powers=[ImbalancedPower:1]
    - [1] `BowlbugSilk` 42/42 block=0 → Debuff
    - [2] `BowlbugEgg` 22/22 block=0 → Attack 7×1 + Defend

  → play card [0]
  → play card [0]
  → play card [0]
  → play card [0] target=0
  → end_turn → round transition
#### Round 2  (e=3/3 block=0 draw=3 disc=5)

  - hand:
    - [0] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [1] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [2] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [3] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [4] `DefendIronclad` cost=1 canPlay=True target=Self
  - enemies:
    - [0] `BowlbugRock` 36/46 block=0 → Attack 15×1  powers=[ImbalancedPower:1]
    - [1] `BowlbugSilk` 42/42 block=0 → Attack 4×2
    - [2] `BowlbugEgg` 22/22 block=7 → Attack 7×1 + Defend
  - player powers: RagePower:3, WeakPower:1

  → play card [0] target=0
  → play card [0] target=0
  → play card [0] target=0
  → end_turn → round transition
#### Round 3  (e=3/3 block=0 draw=8 disc=0)

  - hand:
    - [0] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [1] `Bash` cost=2 canPlay=True target=AnyEnemy
    - [2] `Bloodletting` cost=0 canPlay=True target=Self
    - [3] `DefendIronclad` cost=1 canPlay=True target=Self
    - [4] `DefendIronclad` cost=1 canPlay=True target=Self
  - enemies:
    - [0] `BowlbugRock` 9/46 block=0 → Attack 15×1  powers=[ImbalancedPower:1]
    - [1] `BowlbugSilk` 42/42 block=0 → Debuff
    - [2] `BowlbugEgg` 22/22 block=7 → Attack 7×1 + Defend
  - player powers: RagePower:3, WeakPower:1

  → play card [0] target=0
  → play card [0] target=0
  → play card [0]
  → play card [0]
  → play card [0]
  → end_turn → round transition
#### Round 4  (e=3/3 block=0 draw=3 disc=5)

  - hand:
    - [0] `DefendIronclad` cost=1 canPlay=True target=Self
    - [1] `DefendIronclad` cost=1 canPlay=True target=Self
    - [2] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [3] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [4] `Rage` cost=0 canPlay=True target=Self
  - enemies:
    - [0] `BowlbugSilk` 34/42 block=0 → Attack 4×2  powers=[VulnerablePower:1]
    - [1] `BowlbugEgg` 22/22 block=7 → Attack 7×1 + Defend
  - player powers: RagePower:3, WeakPower:2

  → play card [0]
  → play card [0]
  → play card [0] target=0
  → play card [1]
  → end_turn → round transition
#### Round 5  (e=3/3 block=0 draw=8 disc=0)

  - hand:
    - [0] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [1] `BodySlam` cost=1 canPlay=True target=AnyEnemy
    - [2] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [3] `Rage` cost=0 canPlay=True target=Self
    - [4] `DefendIronclad` cost=1 canPlay=True target=Self
  - enemies:
    - [0] `BowlbugSilk` 21/42 block=0 → Debuff
    - [1] `BowlbugEgg` 22/22 block=7 → Attack 7×1 + Defend
  - player powers: RagePower:6, WeakPower:2

  → play card [0] target=0
  → play card [0] target=0
  → play card [0] target=0
  → play card [0]
  → end_turn → round transition
#### Round 6  (e=3/3 block=0 draw=3 disc=5)

  - hand:
    - [0] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [1] `Bash` cost=2 canPlay=True target=AnyEnemy
    - [2] `Bloodletting` cost=0 canPlay=True target=Self
    - [3] `DefendIronclad` cost=1 canPlay=True target=Self
    - [4] `DefendIronclad` cost=1 canPlay=True target=Self
  - enemies:
    - [0] `BowlbugSilk` 3/42 block=0 → Attack 4×2
    - [1] `BowlbugEgg` 22/22 block=7 → Attack 7×1 + Defend
  - player powers: RagePower:9, WeakPower:3

  → play card [0] target=0
  → play card [0] target=0
  → play card [0]
  → play card [0]
  → play card [0]
  → end_turn → round transition
#### Round 7  (e=3/3 block=0 draw=8 disc=0)

  - hand:
    - [0] `DefendIronclad` cost=1 canPlay=True target=Self
    - [1] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [2] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [3] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [4] `Bash` cost=2 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `BowlbugEgg` 21/22 block=7 → Attack 7×1 + Defend  powers=[VulnerablePower:1]
  - player powers: RagePower:9, WeakPower:3

  → play card [0]
  → play card [0] target=0
  → play card [0] target=0
  → end_turn → round transition
#### Round 8  (e=3/3 block=0 draw=3 disc=5)

  - hand:
    - [0] `Bloodletting` cost=0 canPlay=True target=Self
    - [1] `DefendIronclad` cost=1 canPlay=True target=Self
    - [2] `Rage` cost=0 canPlay=True target=Self
    - [3] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [4] `BodySlam` cost=1 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `BowlbugEgg` 2/22 block=7 → Attack 7×1 + Defend
  - player powers: RagePower:9, WeakPower:3

  → play card [0]
  → play card [0]
  → play card [0]
  → play card [0] target=0
  - rewards offered:
    - [0] gold 14g  canSkip=False
    - [1] card cards=[`Thunderclap`(cost=1), `Bloodletting`(cost=0), `PerfectedStrike`(cost=2)]  canSkip=True

  - rewards offered:
    - [0] gold 14g  canSkip=False
    - [1] card cards=[`Thunderclap`(cost=1), `Bloodletting`(cost=0), `PerfectedStrike`(cost=2)]  canSkip=True

  → claim reward [0] → hp=15 room=CombatRoom
  - rewards offered:
    - [0] card cards=[`Thunderclap`(cost=1), `Bloodletting`(cost=0), `PerfectedStrike`(cost=2)]  canSkip=True

  - rewards offered:
    - [0] card cards=[`Thunderclap`(cost=1), `Bloodletting`(cost=0), `PerfectedStrike`(cost=2)]  canSkip=True

## Floor 12: MapRoom  (hp=15)

  - map options: (0,12):Monster

  → skip reward [0] → hp=15 room=MapRoom
  - combat ended (hp=15)

  - heal → hp=80/80

## Floor 13: CombatRoom  (hp=80)


  → pick map (0,12) → CombatRoom floor=13
### Combat #1 on floor 13

#### Round 1  (e=3/3 block=0 draw=8 disc=0)

  - hand:
    - [0] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [1] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [2] `BodySlam` cost=1 canPlay=True target=AnyEnemy
    - [3] `DefendIronclad` cost=1 canPlay=True target=Self
    - [4] `DefendIronclad` cost=1 canPlay=True target=Self
  - enemies:
    - [0] `LouseProgenitor` 136/136 block=0 → Attack 9×1 + Debuff  powers=[CurlUpPower:14]

  → play card [0] target=0
  → play card [0] target=0
  → play card [0] target=0
  → end_turn → round transition
#### Round 2  (e=3/3 block=0 draw=3 disc=5)

  - hand:
    - [0] `DefendIronclad` cost=1 canPlay=True target=Self
    - [1] `Rage` cost=0 canPlay=True target=Self
    - [2] `Bash` cost=2 canPlay=True target=AnyEnemy
    - [3] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [4] `DefendIronclad` cost=1 canPlay=True target=Self
  - enemies:
    - [0] `LouseProgenitor` 127/136 block=0 → Defend + Buff
  - player powers: FrailPower:2

  → play card [0]
  → play card [0]
  → play card [0] target=0
  → end_turn → round transition
#### Round 3  (e=3/3 block=0 draw=8 disc=0)

  - hand:
    - [0] `Bloodletting` cost=0 canPlay=True target=Self
    - [1] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [2] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [3] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [4] `BodySlam` cost=1 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `LouseProgenitor` 119/136 block=14 → Attack 14×1  powers=[VulnerablePower:1,StrengthPower:5]
  - player powers: FrailPower:2, RagePower:3

  → play card [0]
  → play card [0] target=0
  → play card [0] target=0
  → play card [0] target=0
  → play card [0] target=0
  → end_turn → round transition
#### Round 4  (e=3/3 block=0 draw=3 disc=5)

  - hand:
    - [0] `DefendIronclad` cost=1 canPlay=True target=Self
    - [1] `DefendIronclad` cost=1 canPlay=True target=Self
    - [2] `Rage` cost=0 canPlay=True target=Self
    - [3] `DefendIronclad` cost=1 canPlay=True target=Self
    - [4] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `LouseProgenitor` 94/136 block=0 → Attack 9×1 + Debuff  powers=[StrengthPower:5]
  - player powers: FrailPower:2, RagePower:3

  → play card [0]
  → play card [0]
  → play card [0]
  → play card [0]
  → end_turn → round transition
#### Round 5  (e=3/3 block=0 draw=8 disc=0)

  - hand:
    - [0] `Bash` cost=2 canPlay=True target=AnyEnemy
    - [1] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [2] `DefendIronclad` cost=1 canPlay=True target=Self
    - [3] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [4] `Bloodletting` cost=0 canPlay=True target=Self
  - enemies:
    - [0] `LouseProgenitor` 94/136 block=0 → Defend + Buff  powers=[StrengthPower:5]
  - player powers: FrailPower:4, RagePower:6

  → play card [0] target=0
  → play card [0] target=0
  → play card [2]
  → play card [0]
  → play card [0] target=0
  → end_turn → round transition
#### Round 6  (e=3/3 block=0 draw=3 disc=5)

  - hand:
    - [0] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [1] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [2] `Rage` cost=0 canPlay=True target=Self
    - [3] `BodySlam` cost=1 canPlay=True target=AnyEnemy
    - [4] `DefendIronclad` cost=1 canPlay=True target=Self
  - enemies:
    - [0] `LouseProgenitor` 60/136 block=14 → Attack 14×1  powers=[StrengthPower:10,VulnerablePower:1]
  - player powers: FrailPower:4, RagePower:6

  → play card [0] target=0
  → play card [0] target=0
  → play card [0]
  → play card [0] target=0
  → end_turn → round transition
#### Round 7  (e=3/3 block=0 draw=8 disc=0)

  - hand:
    - [0] `DefendIronclad` cost=1 canPlay=True target=Self
    - [1] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [2] `DefendIronclad` cost=1 canPlay=True target=Self
    - [3] `Bloodletting` cost=0 canPlay=True target=Self
    - [4] `Rage` cost=0 canPlay=True target=Self
  - enemies:
    - [0] `LouseProgenitor` 48/136 block=0 → Attack 9×1 + Debuff  powers=[StrengthPower:10]
  - player powers: FrailPower:4, RagePower:9

  → play card [0]
  → play card [0] target=0
  → play card [0]
  → play card [0]
  → play card [0]
  → end_turn → round transition
#### Round 8  (e=3/3 block=0 draw=3 disc=5)

  - hand:
    - [0] `Bash` cost=2 canPlay=True target=AnyEnemy
    - [1] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [2] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [3] `BodySlam` cost=1 canPlay=True target=AnyEnemy
    - [4] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `LouseProgenitor` 39/136 block=0 → Defend + Buff  powers=[StrengthPower:10]
  - player powers: FrailPower:6, RagePower:12

  → play card [0] target=0
  → play card [0] target=0
  → end_turn → round transition
#### Round 9  (e=3/3 block=0 draw=8 disc=0)

  - hand:
    - [0] `DefendIronclad` cost=1 canPlay=True target=Self
    - [1] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [2] `DefendIronclad` cost=1 canPlay=True target=Self
    - [3] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [4] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `LouseProgenitor` 18/136 block=14 → Attack 14×1  powers=[StrengthPower:15,VulnerablePower:1]
  - player powers: FrailPower:6, RagePower:12

  → play card [0]
  → play card [0] target=0
  → play card [0]
  → end_turn → round transition
  - rewards offered:
    - [0] gold 19g  canSkip=False
    - [1] potion potion=`LiquidMemories`  canSkip=False
    - [2] card cards=[`BodySlam`(cost=0), `ShrugItOff`(cost=1), `FightMe`(cost=2)]  canSkip=True

  - rewards offered:
    - [0] gold 19g  canSkip=False
    - [1] potion potion=`LiquidMemories`  canSkip=False
    - [2] card cards=[`BodySlam`(cost=0), `ShrugItOff`(cost=1), `FightMe`(cost=2)]  canSkip=True

  - heal → hp=80/80

  - rewards offered:
    - [0] gold 19g  canSkip=False
    - [1] potion potion=`LiquidMemories`  canSkip=False
    - [2] card cards=[`BodySlam`(cost=0), `ShrugItOff`(cost=1), `FightMe`(cost=2)]  canSkip=True

  → claim reward [0] → hp=80 room=CombatRoom
  - rewards offered:
    - [0] potion potion=`LiquidMemories`  canSkip=False
    - [1] card cards=[`BodySlam`(cost=0), `ShrugItOff`(cost=1), `FightMe`(cost=2)]  canSkip=True

  - rewards offered:
    - [0] potion potion=`LiquidMemories`  canSkip=False
    - [1] card cards=[`BodySlam`(cost=0), `ShrugItOff`(cost=1), `FightMe`(cost=2)]  canSkip=True

  → claim reward [0] → hp=80 room=CombatRoom
  - rewards offered:
    - [0] card cards=[`BodySlam`(cost=0), `ShrugItOff`(cost=1), `FightMe`(cost=2)]  canSkip=True

  - rewards offered:
    - [0] card cards=[`BodySlam`(cost=0), `ShrugItOff`(cost=1), `FightMe`(cost=2)]  canSkip=True

## Floor 13: MapRoom  (hp=80)

  - map options: (0,13):Elite, (1,13):Monster

  → skip reward [0] → hp=80 room=MapRoom
  - combat ended (hp=80)

## Floor 14: CombatRoom  (hp=80)


  → pick map (1,13) → CombatRoom floor=14
### Combat #1 on floor 14

#### Round 1  (e=3/3 block=0 draw=8 disc=0)

  - hand:
    - [0] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [1] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [2] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [3] `Rage` cost=0 canPlay=True target=Self
    - [4] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `Ovicopter` 130/130 block=0 → Unknown

  → play card [0] target=0
  → play card [0] target=0
  → play card [0] target=0
  → play card [0]
  → end_turn → round transition
#### Round 2  (e=3/3 block=0 draw=3 disc=5)

  - hand:
    - [0] `BodySlam` cost=1 canPlay=True target=AnyEnemy
    - [1] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [2] `Bash` cost=2 canPlay=True target=AnyEnemy
    - [3] `DefendIronclad` cost=1 canPlay=True target=Self
    - [4] `DefendIronclad` cost=1 canPlay=True target=Self
  - enemies:
    - [0] `ToughEgg` 18/18 block=0 → Unknown  powers=[HatchPower:1,MinionPower:1]
    - [1] `ToughEgg` 17/17 block=0 → Unknown  powers=[HatchPower:1,MinionPower:1]
    - [2] `ToughEgg` 15/15 block=0 → Unknown  powers=[HatchPower:1,MinionPower:1]
    - [3] `Ovicopter` 103/130 block=0 → Attack 16×1
  - player powers: RagePower:3

  → play card [0] target=0
  → play card [0] target=0
  → play card [1]
  → end_turn → round transition
#### Round 3  (e=3/3 block=0 draw=8 disc=0)

  - hand:
    - [0] `Bloodletting` cost=0 canPlay=True target=Self
    - [1] `DefendIronclad` cost=1 canPlay=True target=Self
    - [2] `DefendIronclad` cost=1 canPlay=True target=Self
    - [3] `DefendIronclad` cost=1 canPlay=True target=Self
    - [4] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `ToughEgg` 19/19 block=0 → Attack 4×1  powers=[MinionPower:1]
    - [1] `ToughEgg` 21/21 block=0 → Attack 4×1  powers=[MinionPower:1]
    - [2] `ToughEgg` 19/19 block=0 → Attack 4×1  powers=[MinionPower:1]
    - [3] `Ovicopter` 103/130 block=0 → Attack 7×1 + Debuff
  - player powers: RagePower:3

  → play card [0]
  → play card [0]
  → play card [0]
  → play card [0]
  → play card [0] target=0
  → end_turn → round transition
#### Round 4  (e=3/3 block=0 draw=3 disc=5)

  - hand:
    - [0] `DefendIronclad` cost=1 canPlay=True target=Self
    - [1] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [2] `Bash` cost=2 canPlay=True target=AnyEnemy
    - [3] `Rage` cost=0 canPlay=True target=Self
    - [4] `BodySlam` cost=1 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `ToughEgg` 10/19 block=0 → Attack 4×1  powers=[MinionPower:1]
    - [1] `ToughEgg` 21/21 block=0 → Attack 4×1  powers=[MinionPower:1]
    - [2] `ToughEgg` 19/19 block=0 → Attack 4×1  powers=[MinionPower:1]
    - [3] `Ovicopter` 103/130 block=0 → Buff
  - player powers: RagePower:3, VulnerablePower:2

  → play card [0]
  → play card [0] target=0
  → play card [1]
  → play card [1] target=0
  → end_turn → round transition
#### Round 5  (e=3/3 block=0 draw=8 disc=0)

  - hand:
    - [0] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [1] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [2] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [3] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [4] `DefendIronclad` cost=1 canPlay=True target=Self
  - enemies:
    - [0] `ToughEgg` 21/21 block=0 → Attack 4×1  powers=[MinionPower:1]
    - [1] `ToughEgg` 19/19 block=0 → Attack 4×1  powers=[MinionPower:1]
    - [2] `Ovicopter` 103/130 block=0 → Attack 16×1  powers=[StrengthPower:3]
  - player powers: RagePower:6, VulnerablePower:2

  → play card [0] target=0
  → play card [0] target=0
  → play card [0] target=0
  → end_turn → round transition
#### Round 6  (e=3/3 block=0 draw=3 disc=5)

  - hand:
    - [0] `Bash` cost=2 canPlay=True target=AnyEnemy
    - [1] `BodySlam` cost=1 canPlay=True target=AnyEnemy
    - [2] `Bloodletting` cost=0 canPlay=True target=Self
    - [3] `Rage` cost=0 canPlay=True target=Self
    - [4] `DefendIronclad` cost=1 canPlay=True target=Self
  - enemies:
    - [0] `ToughEgg` 19/19 block=0 → Attack 4×1  powers=[MinionPower:1]
    - [1] `Ovicopter` 103/130 block=0 → Attack 7×1 + Debuff  powers=[StrengthPower:3]
  - player powers: RagePower:6, VulnerablePower:2

  → play card [0] target=0
  → play card [0] target=0
  → play card [0]
  → play card [0]
  → play card [0]
  → end_turn → round transition
#### Round 7  (e=3/3 block=0 draw=8 disc=0)

  - hand:
    - [0] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [1] `DefendIronclad` cost=1 canPlay=True target=Self
    - [2] `DefendIronclad` cost=1 canPlay=True target=Self
    - [3] `Bloodletting` cost=0 canPlay=True target=Self
    - [4] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `ToughEgg` 11/19 block=0 → Attack 4×1  powers=[MinionPower:1,VulnerablePower:1]
    - [1] `Ovicopter` 103/130 block=0 → Unknown  powers=[StrengthPower:3]
  - player powers: RagePower:9, VulnerablePower:4

  → play card [0] target=0
  → play card [0]
  → play card [0]
  → play card [0]
  → play card [0] target=0
  → end_turn → round transition
#### Round 8  (e=3/3 block=0 draw=3 disc=5)

  - hand:
    - [0] `BodySlam` cost=1 canPlay=True target=AnyEnemy
    - [1] `Bash` cost=2 canPlay=True target=AnyEnemy
    - [2] `DefendIronclad` cost=1 canPlay=True target=Self
    - [3] `Rage` cost=0 canPlay=True target=Self
    - [4] `DefendIronclad` cost=1 canPlay=True target=Self
  - enemies:
    - [0] `ToughEgg` 14/14 block=0 → Unknown  powers=[HatchPower:1,MinionPower:1]
    - [1] `ToughEgg` 15/15 block=0 → Unknown  powers=[HatchPower:1,MinionPower:1]
    - [2] `ToughEgg` 17/17 block=0 → Unknown  powers=[HatchPower:1,MinionPower:1]
    - [3] `Ovicopter` 94/130 block=0 → Attack 16×1  powers=[StrengthPower:3]
  - player powers: RagePower:9, VulnerablePower:4

  → play card [0] target=0
  → play card [0] target=0
  → play card [1]
  → end_turn → round transition
#### Round 9  (e=3/3 block=0 draw=8 disc=0)

  - hand:
    - [0] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [1] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [2] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [3] `DefendIronclad` cost=1 canPlay=True target=Self
    - [4] `DefendIronclad` cost=1 canPlay=True target=Self
  - enemies:
    - [0] `ToughEgg` 19/19 block=0 → Attack 4×1  powers=[MinionPower:1]
    - [1] `ToughEgg` 20/20 block=0 → Attack 4×1  powers=[MinionPower:1]
    - [2] `ToughEgg` 19/19 block=0 → Attack 4×1  powers=[MinionPower:1]
    - [3] `Ovicopter` 94/130 block=0 → Attack 7×1 + Debuff  powers=[StrengthPower:3]
  - player powers: RagePower:12, VulnerablePower:4

  → play card [0] target=0
  → play card [0] target=0
  → play card [0] target=0
  → end_turn → round transition
  - rewards offered:
    - [0] gold 17g  canSkip=False
    - [1] potion potion=`ColorlessPotion`  canSkip=False
    - [2] card cards=[`Hemokinesis`(cost=1), `Rampage`(cost=1), `Tremble`(cost=1)]  canSkip=True

  - rewards offered:
    - [0] gold 17g  canSkip=False
    - [1] potion potion=`ColorlessPotion`  canSkip=False
    - [2] card cards=[`Hemokinesis`(cost=1), `Rampage`(cost=1), `Tremble`(cost=1)]  canSkip=True

  - heal → hp=80/80

  - rewards offered:
    - [0] gold 17g  canSkip=False
    - [1] potion potion=`ColorlessPotion`  canSkip=False
    - [2] card cards=[`Hemokinesis`(cost=1), `Rampage`(cost=1), `Tremble`(cost=1)]  canSkip=True

  → claim reward [0] → hp=80 room=CombatRoom
  - rewards offered:
    - [0] potion potion=`ColorlessPotion`  canSkip=False
    - [1] card cards=[`Hemokinesis`(cost=1), `Rampage`(cost=1), `Tremble`(cost=1)]  canSkip=True

  - rewards offered:
    - [0] potion potion=`ColorlessPotion`  canSkip=False
    - [1] card cards=[`Hemokinesis`(cost=1), `Rampage`(cost=1), `Tremble`(cost=1)]  canSkip=True

  → claim reward [0] → hp=80 room=CombatRoom
  - rewards offered:
    - [0] card cards=[`Hemokinesis`(cost=1), `Rampage`(cost=1), `Tremble`(cost=1)]  canSkip=True

  - rewards offered:
    - [0] card cards=[`Hemokinesis`(cost=1), `Rampage`(cost=1), `Tremble`(cost=1)]  canSkip=True

## Floor 14: MapRoom  (hp=80)

  - map options: (1,14):RestSite

  → skip reward [0] → hp=80 room=MapRoom
  - combat ended (hp=80)

## Floor 15: RestSiteRoom  (hp=80)


  → pick map (1,14) → RestSiteRoom floor=15
## Floor 15: MapRoom  (hp=80)

  - map options: (3,15):Boss

  → pick rest option [1] → MapRoom hp=80
## Floor 16: BossRoom  (hp=80)


  → pick map (3,15) → BossRoom floor=16
### Combat #1 on floor 16

#### Round 1  (e=3/3 block=0 draw=8 disc=0)

  - hand:
    - [0] `BodySlam` cost=1 canPlay=True target=AnyEnemy
    - [1] `DefendIronclad` cost=1 canPlay=True target=Self
    - [2] `DefendIronclad` cost=1 canPlay=True target=Self
    - [3] `DefendIronclad` cost=1 canPlay=True target=Self
    - [4] `Bash` cost=2 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `KnowledgeDemon` 379/379 block=0 → Debuff

  → play card [0] target=0
  → play card [0]
  → play card [0]
  → end_turn → round transition
#### Round 2  (e=3/3 block=0 draw=3 disc=5)

  - hand:
    - [0] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [1] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [2] `DefendIronclad` cost=1 canPlay=True target=Self
    - [3] `Rage` cost=0 canPlay=True target=Self
    - [4] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `KnowledgeDemon` 379/379 block=0 → Attack 17×1

  → play card [0] target=0
  → play card [0] target=0
  → play card [0]
  → play card [0]
  → end_turn → round transition
#### Round 3  (e=3/3 block=0 draw=8 disc=0)

  - hand:
    - [0] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [1] `Bloodletting` cost=0 canPlay=True target=Self
    - [2] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [3] `Bash` cost=2 canPlay=True target=AnyEnemy
    - [4] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `KnowledgeDemon` 361/379 block=0 → Attack 8×3
  - player powers: RagePower:3

  → play card [0] target=0
  → play card [0]
  → play card [0] target=0
  → play card [0] target=0
  → play card [0] target=0
  → end_turn → round transition
#### Round 4  (e=3/3 block=0 draw=3 disc=5)

  - hand:
    - [0] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [1] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [2] `DefendIronclad` cost=1 canPlay=True target=Self
    - [3] `DefendIronclad` cost=1 canPlay=True target=Self
    - [4] `BodySlam` cost=1 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `KnowledgeDemon` 322/379 block=0 → Attack 11×1 + Unknown + Buff  powers=[VulnerablePower:1]
  - player powers: RagePower:3

  → play card [0] target=0
  → play card [0] target=0
  → play card [0]
  → end_turn → round transition
#### Round 5  (e=3/3 block=0 draw=8 disc=0)

  - hand:
    - [0] `DefendIronclad` cost=1 canPlay=True target=Self
    - [1] `Rage` cost=0 canPlay=True target=Self
    - [2] `DefendIronclad` cost=1 canPlay=True target=Self
    - [3] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [4] `DefendIronclad` cost=1 canPlay=True target=Self
  - enemies:
    - [0] `KnowledgeDemon` 326/379 block=0 → Debuff  powers=[StrengthPower:2]
  - player powers: RagePower:3

  → play card [0]
  → play card [0]
  → play card [0]
  → play card [0] target=0
  → end_turn → round transition
#### Round 6  (e=3/3 block=0 draw=3 disc=5)

  - hand:
    - [0] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [1] `BodySlam` cost=1 canPlay=True target=AnyEnemy
    - [2] `Bash` cost=2 canPlay=True target=AnyEnemy
    - [3] `DefendIronclad` cost=1 canPlay=True target=Self
    - [4] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `KnowledgeDemon` 317/379 block=0 → Attack 17×1  powers=[StrengthPower:2]
  - player powers: RagePower:6

  → play card [0] target=0
  → play card [0] target=0
  → play card [1]
  → end_turn → round transition
#### Round 7  (e=3/3 block=0 draw=8 disc=0)

  - hand:
    - [0] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [1] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [2] `Bloodletting` cost=0 canPlay=True target=Self
    - [3] `DefendIronclad` cost=1 canPlay=True target=Self
    - [4] `Bash` cost=2 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `KnowledgeDemon` 308/379 block=0 → Attack 8×3  powers=[StrengthPower:2]
  - player powers: RagePower:6

  → play card [0] target=0
  → play card [0] target=0
  → play card [0]
  → play card [0]
  → play card [0] target=0
  → end_turn → round transition
  - rewards offered:
    - [0] gold 100g  canSkip=False
    - [1] card cards=[`Pyre`(cost=2), `Hellraiser`(cost=2), `Juggernaut`(cost=2)]  canSkip=True

  - rewards offered:
    - [0] gold 100g  canSkip=False
    - [1] card cards=[`Pyre`(cost=2), `Hellraiser`(cost=2), `Juggernaut`(cost=2)]  canSkip=True

  - heal → hp=80/80

  - rewards offered:
    - [0] gold 100g  canSkip=False
    - [1] card cards=[`Pyre`(cost=2), `Hellraiser`(cost=2), `Juggernaut`(cost=2)]  canSkip=True

  → claim reward [0] → hp=80 room=BossRoom
  - rewards offered:
    - [0] card cards=[`Pyre`(cost=2), `Hellraiser`(cost=2), `Juggernaut`(cost=2)]  canSkip=True

  - rewards offered:
    - [0] card cards=[`Pyre`(cost=2), `Hellraiser`(cost=2), `Juggernaut`(cost=2)]  canSkip=True

## Floor 16: MapRoom  (hp=80)


  → skip reward [0] → hp=80 room=MapRoom
  - combat ended (hp=80)

## Floor 0: MapRoom  (hp=80)

  - map options: (3,0):Unknown, (1,1):Monster, (2,1):Monster, (4,1):Monster

  → enter_next_act → MapRoom floor=0
## Floor 2: CombatRoom  (hp=80)


  → pick map (1,1) → CombatRoom floor=2
### Combat #1 on floor 2

#### Round 1  (e=3/3 block=0 draw=8 disc=0)

  - hand:
    - [0] `DefendIronclad` cost=1 canPlay=True target=Self
    - [1] `DefendIronclad` cost=1 canPlay=True target=Self
    - [2] `Rage` cost=0 canPlay=True target=Self
    - [3] `BodySlam` cost=1 canPlay=True target=AnyEnemy
    - [4] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `LivingShield` 55/55 block=0 → Attack 6×1  powers=[RampartPower:25]
    - [1] `TurretOperator` 41/41 block=25 → Attack 3×5

  → play card [0]
  → play card [0]
  → play card [0]
  → play card [0] target=0
  → end_turn → round transition
#### Round 2  (e=3/3 block=0 draw=3 disc=5)

  - hand:
    - [0] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [1] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [2] `Bash` cost=2 canPlay=True target=AnyEnemy
    - [3] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [4] `DefendIronclad` cost=1 canPlay=True target=Self
  - enemies:
    - [0] `LivingShield` 42/55 block=0 → Attack 6×1  powers=[RampartPower:25]
    - [1] `TurretOperator` 41/41 block=25 → Attack 3×5
  - player powers: RagePower:3

  → play card [0] target=0
  → play card [0] target=0
  → play card [1] target=0
  → end_turn → round transition
#### Round 3  (e=3/3 block=0 draw=8 disc=0)

  - hand:
    - [0] `DefendIronclad` cost=1 canPlay=True target=Self
    - [1] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [2] `Bloodletting` cost=0 canPlay=True target=Self
    - [3] `Bash` cost=2 canPlay=True target=AnyEnemy
    - [4] `DefendIronclad` cost=1 canPlay=True target=Self
  - enemies:
    - [0] `LivingShield` 15/55 block=0 → Attack 6×1  powers=[RampartPower:25]
    - [1] `TurretOperator` 41/41 block=25 → Buff
  - player powers: RagePower:3

  → play card [0]
  → play card [0] target=0
  → play card [0]
  → play card [0] target=0
  → play card [0]
  → end_turn → round transition
#### Round 4  (e=3/3 block=0 draw=3 disc=5)

  - hand:
    - [0] `DefendIronclad` cost=1 canPlay=True target=Self
    - [1] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [2] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [3] `BodySlam` cost=1 canPlay=True target=AnyEnemy
    - [4] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `TurretOperator` 41/41 block=0 → Attack 3×5  powers=[StrengthPower:1]
  - player powers: RagePower:3

  → play card [0]
  → play card [0] target=0
  → play card [0] target=0
  → end_turn → round transition
#### Round 5  (e=3/3 block=0 draw=8 disc=0)

  - hand:
    - [0] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [1] `Rage` cost=0 canPlay=True target=Self
    - [2] `DefendIronclad` cost=1 canPlay=True target=Self
    - [3] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [4] `Bash` cost=2 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `TurretOperator` 23/41 block=0 → Attack 3×5  powers=[StrengthPower:1]
  - player powers: RagePower:3

  → play card [0] target=0
  → play card [0]
  → play card [0]
  → play card [0] target=0
  → end_turn → round transition
#### Round 6  (e=3/3 block=0 draw=3 disc=5)

  - hand:
    - [0] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [1] `DefendIronclad` cost=1 canPlay=True target=Self
    - [2] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [3] `Bloodletting` cost=0 canPlay=True target=Self
    - [4] `DefendIronclad` cost=1 canPlay=True target=Self
  - enemies:
    - [0] `TurretOperator` 5/41 block=0 → Buff  powers=[StrengthPower:1]
  - player powers: RagePower:6

  → play card [0] target=0
  - rewards offered:
    - [0] gold 19g  canSkip=False
    - [1] card cards=[`FlameBarrier`(cost=2), `StoneArmor`(cost=1), `Pillage`(cost=1)]  canSkip=True

  - rewards offered:
    - [0] gold 19g  canSkip=False
    - [1] card cards=[`FlameBarrier`(cost=2), `StoneArmor`(cost=1), `Pillage`(cost=1)]  canSkip=True

  → claim reward [0] → hp=21 room=CombatRoom
  - rewards offered:
    - [0] card cards=[`FlameBarrier`(cost=2), `StoneArmor`(cost=1), `Pillage`(cost=1)]  canSkip=True

  - rewards offered:
    - [0] card cards=[`FlameBarrier`(cost=2), `StoneArmor`(cost=1), `Pillage`(cost=1)]  canSkip=True

## Floor 2: MapRoom  (hp=21)

  - map options: (0,2):Unknown

  → skip reward [0] → hp=21 room=MapRoom
  - combat ended (hp=21)

  - heal → hp=80/80

## Floor 3: MerchantRoom  (hp=80)


  → pick map (0,2) → MerchantRoom floor=3
## Floor 3: MapRoom  (hp=80)

  - map options: (0,3):Monster

  → leave merchant → MapRoom
## Floor 4: CombatRoom  (hp=80)


  → pick map (0,3) → CombatRoom floor=4
### Combat #1 on floor 4

#### Round 1  (e=3/3 block=0 draw=8 disc=0)

  - hand:
    - [0] `DefendIronclad` cost=1 canPlay=True target=Self
    - [1] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [2] `DefendIronclad` cost=1 canPlay=True target=Self
    - [3] `Rage` cost=0 canPlay=True target=Self
    - [4] `Bloodletting` cost=0 canPlay=True target=Self
  - enemies:
    - [0] `DevotedSculptor` 162/162 block=0 → Buff

  → play card [0]
  → play card [0] target=0
  → play card [0]
  → play card [0]
  → play card [0]
  → end_turn → round transition
#### Round 2  (e=3/3 block=0 draw=3 disc=5)

  - hand:
    - [0] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [1] `Bash` cost=2 canPlay=True target=AnyEnemy
    - [2] `DefendIronclad` cost=1 canPlay=True target=Self
    - [3] `DefendIronclad` cost=1 canPlay=True target=Self
    - [4] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `DevotedSculptor` 153/162 block=0 → Attack 12×1  powers=[RitualPower:9]
  - player powers: RagePower:3

  → play card [0] target=0
  → play card [0] target=0
  → end_turn → round transition
#### Round 3  (e=3/3 block=0 draw=8 disc=0)

  - hand:
    - [0] `BodySlam` cost=1 canPlay=True target=AnyEnemy
    - [1] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [2] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [3] `DefendIronclad` cost=1 canPlay=True target=Self
    - [4] `Bash` cost=2 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `DevotedSculptor` 136/162 block=0 → Attack 12×1  powers=[RitualPower:9,VulnerablePower:1,StrengthPower:9]
  - player powers: RagePower:3

  → play card [0] target=0
  → play card [0] target=0
  → play card [0] target=0
  → end_turn → round transition
#### Round 4  (e=3/3 block=0 draw=3 disc=5)

  - hand:
    - [0] `DefendIronclad` cost=1 canPlay=True target=Self
    - [1] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [2] `Rage` cost=0 canPlay=True target=Self
    - [3] `DefendIronclad` cost=1 canPlay=True target=Self
    - [4] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `DevotedSculptor` 110/162 block=0 → Attack 12×1  powers=[RitualPower:9,StrengthPower:18]
  - player powers: RagePower:3

  → play card [0]
  → play card [0] target=0
  → play card [0]
  → play card [0]
  → end_turn → round transition
#### Round 5  (e=3/3 block=0 draw=8 disc=0)

  - hand:
    - [0] `DefendIronclad` cost=1 canPlay=True target=Self
    - [1] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [2] `Bloodletting` cost=0 canPlay=True target=Self
    - [3] `DefendIronclad` cost=1 canPlay=True target=Self
    - [4] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `DevotedSculptor` 101/162 block=0 → Attack 12×1  powers=[RitualPower:9,StrengthPower:27]
  - player powers: RagePower:6

  → play card [0]
  → play card [0] target=0
  → play card [0]
  → play card [0]
  → play card [0] target=0
  → end_turn → round transition
  - rewards offered:
    - [0] gold 19g  canSkip=False
    - [1] potion potion=`ExplosiveAmpoule`  canSkip=False
    - [2] card cards=[`TwinStrike`(cost=1), `InfernalBlade`(cost=1), `BloodWall`(cost=2)]  canSkip=True

  - rewards offered:
    - [0] gold 19g  canSkip=False
    - [1] potion potion=`ExplosiveAmpoule`  canSkip=False
    - [2] card cards=[`TwinStrike`(cost=1), `InfernalBlade`(cost=1), `BloodWall`(cost=2)]  canSkip=True

  - heal → hp=80/80

  - rewards offered:
    - [0] gold 19g  canSkip=False
    - [1] potion potion=`ExplosiveAmpoule`  canSkip=False
    - [2] card cards=[`TwinStrike`(cost=1), `InfernalBlade`(cost=1), `BloodWall`(cost=2)]  canSkip=True

  → claim reward [0] → hp=80 room=CombatRoom
  - rewards offered:
    - [0] potion potion=`ExplosiveAmpoule`  canSkip=False
    - [1] card cards=[`TwinStrike`(cost=1), `InfernalBlade`(cost=1), `BloodWall`(cost=2)]  canSkip=True

  - rewards offered:
    - [0] potion potion=`ExplosiveAmpoule`  canSkip=False
    - [1] card cards=[`TwinStrike`(cost=1), `InfernalBlade`(cost=1), `BloodWall`(cost=2)]  canSkip=True

  → claim reward [0] → hp=80 room=CombatRoom
  - rewards offered:
    - [0] card cards=[`TwinStrike`(cost=1), `InfernalBlade`(cost=1), `BloodWall`(cost=2)]  canSkip=True

  - rewards offered:
    - [0] card cards=[`TwinStrike`(cost=1), `InfernalBlade`(cost=1), `BloodWall`(cost=2)]  canSkip=True

## Floor 4: MapRoom  (hp=80)

  - map options: (0,4):Monster

  → skip reward [0] → hp=80 room=MapRoom
  - combat ended (hp=80)

## Floor 5: CombatRoom  (hp=80)


  → pick map (0,4) → CombatRoom floor=5
### Combat #1 on floor 5

#### Round 1  (e=3/3 block=0 draw=8 disc=0)

  - hand:
    - [0] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [1] `DefendIronclad` cost=1 canPlay=True target=Self
    - [2] `Bash` cost=2 canPlay=True target=AnyEnemy
    - [3] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [4] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `GlobeHead` 148/148 block=0 → Attack 13×1 + Debuff  powers=[GalvanicPower:6]

  → play card [0] target=0
  → play card [0]
  → play card [1] target=0
  → end_turn → round transition
#### Round 2  (e=3/3 block=0 draw=3 disc=5)

  - hand:
    - [0] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [1] `BodySlam` cost=1 canPlay=True target=AnyEnemy
    - [2] `Bloodletting` cost=0 canPlay=True target=Self
    - [3] `Rage` cost=0 canPlay=True target=Self
    - [4] `DefendIronclad` cost=1 canPlay=True target=Self
  - enemies:
    - [0] `GlobeHead` 130/148 block=0 → Attack 6×3  powers=[GalvanicPower:6]
  - player powers: FrailPower:2

  → play card [0] target=0
  → play card [0] target=0
  → play card [0]
  → play card [0]
  → play card [0]
  → end_turn → round transition
#### Round 3  (e=3/3 block=0 draw=8 disc=0)

  - hand:
    - [0] `DefendIronclad` cost=1 canPlay=True target=Self
    - [1] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [2] `DefendIronclad` cost=1 canPlay=True target=Self
    - [3] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [4] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `GlobeHead` 121/148 block=0 → Attack 16×1 + Buff  powers=[GalvanicPower:6]
  - player powers: FrailPower:2, RagePower:3

  → play card [0]
  → play card [0] target=0
  → play card [0]
  → end_turn → round transition
#### Round 4  (e=3/3 block=0 draw=3 disc=5)

  - hand:
    - [0] `Bash` cost=2 canPlay=True target=AnyEnemy
    - [1] `BodySlam` cost=1 canPlay=True target=AnyEnemy
    - [2] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [3] `DefendIronclad` cost=1 canPlay=True target=Self
    - [4] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `GlobeHead` 112/148 block=0 → Attack 13×1 + Debuff  powers=[GalvanicPower:6,StrengthPower:2]
  - player powers: FrailPower:2, RagePower:3

  → play card [0] target=0
  → play card [0] target=0
  → end_turn → round transition
#### Round 5  (e=3/3 block=0 draw=8 disc=0)

  - hand:
    - [0] `Rage` cost=0 canPlay=True target=Self
    - [1] `DefendIronclad` cost=1 canPlay=True target=Self
    - [2] `Bloodletting` cost=0 canPlay=True target=Self
    - [3] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [4] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `GlobeHead` 104/148 block=0 → Attack 6×3  powers=[GalvanicPower:6,StrengthPower:2,VulnerablePower:1]
  - player powers: FrailPower:4, RagePower:3

  → play card [0]
  → play card [0]
  → play card [0]
  → play card [0] target=0
  → play card [0] target=0
  → end_turn → round transition
#### Round 6  (e=3/3 block=0 draw=3 disc=5)

  - hand:
    - [0] `BodySlam` cost=1 canPlay=True target=AnyEnemy
    - [1] `DefendIronclad` cost=1 canPlay=True target=Self
    - [2] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [3] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [4] `Bash` cost=2 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `GlobeHead` 78/148 block=0 → Attack 16×1 + Buff  powers=[GalvanicPower:6,StrengthPower:2]
  - player powers: FrailPower:4, RagePower:6

  → play card [0] target=0
  → play card [0]
  → play card [0] target=0
  → end_turn → round transition
#### Round 7  (e=3/3 block=0 draw=8 disc=0)

  - hand:
    - [0] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [1] `DefendIronclad` cost=1 canPlay=True target=Self
    - [2] `DefendIronclad` cost=1 canPlay=True target=Self
    - [3] `DefendIronclad` cost=1 canPlay=True target=Self
    - [4] `Bash` cost=2 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `GlobeHead` 69/148 block=0 → Attack 13×1 + Debuff  powers=[GalvanicPower:6,StrengthPower:4]
  - player powers: FrailPower:4, RagePower:6

  → play card [0] target=0
  → play card [0]
  → play card [0]
  → end_turn → round transition
  - rewards offered:
    - [0] gold 15g  canSkip=False
    - [1] card cards=[`FiendFire`(cost=2), `Stampede`(cost=2), `Thunderclap`(cost=1)]  canSkip=True

  - rewards offered:
    - [0] gold 15g  canSkip=False
    - [1] card cards=[`FiendFire`(cost=2), `Stampede`(cost=2), `Thunderclap`(cost=1)]  canSkip=True

  - heal → hp=80/80

  - rewards offered:
    - [0] gold 15g  canSkip=False
    - [1] card cards=[`FiendFire`(cost=2), `Stampede`(cost=2), `Thunderclap`(cost=1)]  canSkip=True

  → claim reward [0] → hp=80 room=CombatRoom
  - rewards offered:
    - [0] card cards=[`FiendFire`(cost=2), `Stampede`(cost=2), `Thunderclap`(cost=1)]  canSkip=True

  - rewards offered:
    - [0] card cards=[`FiendFire`(cost=2), `Stampede`(cost=2), `Thunderclap`(cost=1)]  canSkip=True

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

#### Round 1  (e=3/3 block=0 draw=9 disc=0)

  - hand:
    - [0] `DefendIronclad` cost=1 canPlay=True target=Self
    - [1] `Rage` cost=0 canPlay=True target=Self
    - [2] `Bash` cost=2 canPlay=True target=AnyEnemy
    - [3] `Debt` cost=0 canPlay=False target=None
    - [4] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `SoulNexus` 234/234 block=0 → Attack 29×1

  → play card [0]
  → play card [0]
  → play card [0] target=0
  → end_turn → round transition
#### Round 2  (e=3/3 block=0 draw=4 disc=5)

  - hand:
    - [0] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [1] `BodySlam` cost=1 canPlay=True target=AnyEnemy
    - [2] `DefendIronclad` cost=1 canPlay=True target=Self
    - [3] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [4] `DefendIronclad` cost=1 canPlay=True target=Self
  - enemies:
    - [0] `SoulNexus` 226/234 block=0 → Attack 6×4  powers=[VulnerablePower:1]
  - player powers: RagePower:3

  → play card [0] target=0
  → play card [0] target=0
  → play card [0]
  → end_turn → round transition
#### Round 3  (e=3/3 block=0 draw=9 disc=0)

  - hand:
    - [0] `Bloodletting` cost=0 canPlay=True target=Self
    - [1] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [2] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [3] `DefendIronclad` cost=1 canPlay=True target=Self
    - [4] `DefendIronclad` cost=1 canPlay=True target=Self
  - enemies:
    - [0] `SoulNexus` 213/234 block=0 → Attack 29×1
  - player powers: RagePower:3

  → play card [0]
  → play card [0] target=0
  → play card [0] target=0
  → play card [0]
  → play card [0]
  → end_turn → round transition
#### Round 4  (e=3/3 block=0 draw=4 disc=5)

  - hand:
    - [0] `Rage` cost=0 canPlay=True target=Self
    - [1] `Debt` cost=0 canPlay=False target=None
    - [2] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [3] `DefendIronclad` cost=1 canPlay=True target=Self
    - [4] `DefendIronclad` cost=1 canPlay=True target=Self
  - enemies:
    - [0] `SoulNexus` 195/234 block=0 → Attack 6×4
  - player powers: RagePower:3

  → play card [0]
  → play card [1] target=0
  → play card [1]
  → play card [1]
  → end_turn → round transition
#### Round 5  (e=3/3 block=0 draw=9 disc=0)

  - hand:
    - [0] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [1] `BodySlam` cost=1 canPlay=True target=AnyEnemy
    - [2] `Bash` cost=2 canPlay=True target=AnyEnemy
    - [3] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [4] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `SoulNexus` 186/234 block=0 → Attack 29×1
  - player powers: RagePower:6

  → play card [0] target=0
  → play card [0] target=0
  → play card [1] target=0
  → end_turn → round transition
  - rewards offered:
    - [0] gold 40g  canSkip=False
    - [1] potion potion=`StrengthPotion`  canSkip=False
    - [2] relic relic=``  canSkip=False
    - [3] card cards=[`Bully`(cost=0), `PerfectedStrike`(cost=2), `Breakthrough`(cost=1)]  canSkip=True

  - rewards offered:
    - [0] gold 40g  canSkip=False
    - [1] potion potion=`StrengthPotion`  canSkip=False
    - [2] relic relic=``  canSkip=False
    - [3] card cards=[`Bully`(cost=0), `PerfectedStrike`(cost=2), `Breakthrough`(cost=1)]  canSkip=True

  - heal → hp=80/80

  - rewards offered:
    - [0] gold 40g  canSkip=False
    - [1] potion potion=`StrengthPotion`  canSkip=False
    - [2] relic relic=``  canSkip=False
    - [3] card cards=[`Bully`(cost=0), `PerfectedStrike`(cost=2), `Breakthrough`(cost=1)]  canSkip=True

  → claim reward [0] → hp=80 room=CombatRoom
  - rewards offered:
    - [0] potion potion=`StrengthPotion`  canSkip=False
    - [1] relic relic=``  canSkip=False
    - [2] card cards=[`Bully`(cost=0), `PerfectedStrike`(cost=2), `Breakthrough`(cost=1)]  canSkip=True

  - rewards offered:
    - [0] potion potion=`StrengthPotion`  canSkip=False
    - [1] relic relic=``  canSkip=False
    - [2] card cards=[`Bully`(cost=0), `PerfectedStrike`(cost=2), `Breakthrough`(cost=1)]  canSkip=True

  → claim reward [0] → hp=80 room=CombatRoom
  - rewards offered:
    - [0] relic relic=``  canSkip=False
    - [1] card cards=[`Bully`(cost=0), `PerfectedStrike`(cost=2), `Breakthrough`(cost=1)]  canSkip=True

  - rewards offered:
    - [0] relic relic=``  canSkip=False
    - [1] card cards=[`Bully`(cost=0), `PerfectedStrike`(cost=2), `Breakthrough`(cost=1)]  canSkip=True

  → claim reward [0] → hp=80 room=CombatRoom
  - rewards offered:
    - [0] card cards=[`Bully`(cost=0), `PerfectedStrike`(cost=2), `Breakthrough`(cost=1)]  canSkip=True

  - rewards offered:
    - [0] card cards=[`Bully`(cost=0), `PerfectedStrike`(cost=2), `Breakthrough`(cost=1)]  canSkip=True

## Floor 7: MapRoom  (hp=80)

  - map options: (2,7):Treasure

  → skip reward [0] → hp=80 room=MapRoom
  - combat ended (hp=80)

## Floor 8: TreasureRoom  (hp=80)


  → pick map (2,7) → TreasureRoom floor=8
## Floor 8: MapRoom  (hp=80)

  - map options: (1,8):Unknown, (3,8):Elite

  → take treasure → MapRoom relics=`BurningBlood`, `ScrollBoxes`, `OldCoin`, `Gorget`, `JuzuBracelet`, `TeaOfDiscourtesy`, `OddlySmoothStone`, `MealTicket`
## Floor 9: CombatRoom  (hp=80)


  → pick map (3,8) → CombatRoom floor=9
### Combat #1 on floor 9

#### Round 1  (e=3/3 block=0 draw=9 disc=0)

  - hand:
    - [0] `BodySlam` cost=1 canPlay=True target=AnyEnemy
    - [1] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [2] `Rage` cost=0 canPlay=True target=Self
    - [3] `DefendIronclad` cost=1 canPlay=True target=Self
    - [4] `Bash` cost=2 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `MechaKnight` 300/300 block=0 → Attack 25×1  powers=[ArtifactPower:3]

  → play card [0] target=0
  → play card [0] target=0
  → play card [0]
  → play card [0]
  → end_turn → round transition
#### Round 2  (e=3/3 block=0 draw=4 disc=5)

  - hand:
    - [0] `Bloodletting` cost=0 canPlay=True target=Self
    - [1] `DefendIronclad` cost=1 canPlay=True target=Self
    - [2] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [3] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [4] `DefendIronclad` cost=1 canPlay=True target=Self
  - enemies:
    - [0] `MechaKnight` 291/300 block=0 → Unknown  powers=[ArtifactPower:3]
  - player powers: RagePower:3

  → play card [0]
  → play card [0]
  → play card [0] target=0
  → play card [0] target=0
  → play card [0]
  → end_turn → round transition
#### Round 3  (e=3/3 block=0 draw=9 disc=0)

  - hand:
    - [0] `Burn` cost=0 canPlay=False target=None
    - [1] `Burn` cost=0 canPlay=False target=None
    - [2] `Burn` cost=0 canPlay=False target=None
    - [3] `Burn` cost=0 canPlay=False target=None
    - [4] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [5] `DefendIronclad` cost=1 canPlay=True target=Self
    - [6] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [7] `Debt` cost=0 canPlay=False target=None
    - [8] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `MechaKnight` 273/300 block=0 → Defend + Buff  powers=[ArtifactPower:3]
  - player powers: RagePower:3

  → play card [4] target=0
  → play card [4]
  → play card [4] target=0
  → end_turn → round transition
#### Round 4  (e=3/3 block=0 draw=4 disc=9)

  - hand:
    - [0] `DefendIronclad` cost=1 canPlay=True target=Self
    - [1] `BodySlam` cost=1 canPlay=True target=AnyEnemy
    - [2] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [3] `DefendIronclad` cost=1 canPlay=True target=Self
    - [4] `Bloodletting` cost=0 canPlay=True target=Self
  - enemies:
    - [0] `MechaKnight` 255/300 block=15 → Attack 35×1  powers=[ArtifactPower:3,StrengthPower:5]
  - player powers: RagePower:3

  → play card [0]
  → play card [0] target=0
  → play card [0] target=0
  → play card [1]
  → play card [0]
  → end_turn → round transition
#### Round 5  (e=3/3 block=0 draw=13 disc=0)

  - hand:
    - [0] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [1] `Bash` cost=2 canPlay=True target=AnyEnemy
    - [2] `DefendIronclad` cost=1 canPlay=True target=Self
    - [3] `Rage` cost=0 canPlay=True target=Self
    - [4] `BodySlam` cost=1 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `MechaKnight` 255/300 block=0 → Unknown  powers=[ArtifactPower:3,StrengthPower:5]
  - player powers: RagePower:3

  → play card [0] target=0
  → play card [0] target=0
  → play card [1]
  → end_turn → round transition
#### Round 6  (e=3/3 block=0 draw=8 disc=5)

  - hand:
    - [0] `Burn` cost=0 canPlay=False target=None
    - [1] `Burn` cost=0 canPlay=False target=None
    - [2] `Burn` cost=0 canPlay=False target=None
    - [3] `Burn` cost=0 canPlay=False target=None
    - [4] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [5] `Debt` cost=0 canPlay=False target=None
    - [6] `DefendIronclad` cost=1 canPlay=True target=Self
    - [7] `Bloodletting` cost=0 canPlay=True target=Self
    - [8] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `MechaKnight` 238/300 block=0 → Defend + Buff  powers=[ArtifactPower:2,StrengthPower:5]
  - player powers: RagePower:6

  → play card [4] target=0
  → play card [5]
  → play card [5]
  → play card [5] target=0
  → end_turn → round transition
#### Round 7  (e=3/3 block=0 draw=3 disc=14)

  - hand:
    - [0] `DefendIronclad` cost=1 canPlay=True target=Self
    - [1] `Burn` cost=0 canPlay=False target=None
    - [2] `DefendIronclad` cost=1 canPlay=True target=Self
    - [3] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [4] `Burn` cost=0 canPlay=False target=None
  - enemies:
    - [0] `MechaKnight` 220/300 block=15 → Attack 35×1  powers=[ArtifactPower:2,StrengthPower:10]
  - player powers: RagePower:6

  → play card [0]
  → play card [1]
  → play card [1] target=0
  → end_turn → round transition
  - rewards offered:
    - [0] gold 35g  canSkip=False
    - [1] potion potion=`SkillPotion`  canSkip=False
    - [2] relic relic=``  canSkip=False
    - [3] card cards=[`ExpectAFight`(cost=1), `Breakthrough`(cost=1), `PactsEnd`(cost=0)]  canSkip=True

  - rewards offered:
    - [0] gold 35g  canSkip=False
    - [1] potion potion=`SkillPotion`  canSkip=False
    - [2] relic relic=``  canSkip=False
    - [3] card cards=[`ExpectAFight`(cost=1), `Breakthrough`(cost=1), `PactsEnd`(cost=0)]  canSkip=True

  - heal → hp=80/80

  - rewards offered:
    - [0] gold 35g  canSkip=False
    - [1] potion potion=`SkillPotion`  canSkip=False
    - [2] relic relic=``  canSkip=False
    - [3] card cards=[`ExpectAFight`(cost=1), `Breakthrough`(cost=1), `PactsEnd`(cost=0)]  canSkip=True

  → claim reward [0] → hp=80 room=CombatRoom
  - rewards offered:
    - [0] potion potion=`SkillPotion`  canSkip=False
    - [1] relic relic=``  canSkip=False
    - [2] card cards=[`ExpectAFight`(cost=1), `Breakthrough`(cost=1), `PactsEnd`(cost=0)]  canSkip=True

  - rewards offered:
    - [0] potion potion=`SkillPotion`  canSkip=False
    - [1] relic relic=``  canSkip=False
    - [2] card cards=[`ExpectAFight`(cost=1), `Breakthrough`(cost=1), `PactsEnd`(cost=0)]  canSkip=True

  → claim reward [0] → hp=80 room=CombatRoom
  - rewards offered:
    - [0] relic relic=``  canSkip=False
    - [1] card cards=[`ExpectAFight`(cost=1), `Breakthrough`(cost=1), `PactsEnd`(cost=0)]  canSkip=True

  - rewards offered:
    - [0] relic relic=``  canSkip=False
    - [1] card cards=[`ExpectAFight`(cost=1), `Breakthrough`(cost=1), `PactsEnd`(cost=0)]  canSkip=True

  → claim reward [0] → hp=80 room=CombatRoom
  - rewards offered:
    - [0] card cards=[`ExpectAFight`(cost=1), `Breakthrough`(cost=1), `PactsEnd`(cost=0)]  canSkip=True

  - rewards offered:
    - [0] card cards=[`ExpectAFight`(cost=1), `Breakthrough`(cost=1), `PactsEnd`(cost=0)]  canSkip=True

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

#### Round 1  (e=3/3 block=0 draw=9 disc=0)

  - hand:
    - [0] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [1] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [2] `DefendIronclad` cost=1 canPlay=True target=Self
    - [3] `BodySlam` cost=1 canPlay=True target=AnyEnemy
    - [4] `Rage` cost=0 canPlay=True target=Self
  - enemies:
    - [0] `PunchConstruct` 55/55 block=0 → Defend  powers=[ArtifactPower:1]
    - [1] `CubexConstruct` 65/65 block=0 → Buff  powers=[ArtifactPower:1]
    - [2] `CubexConstruct` 65/65 block=0 → Buff  powers=[ArtifactPower:1]

  → play card [0] target=0
  → play card [0] target=0
  → play card [0]
  → play card [1]
  → end_turn → round transition
#### Round 2  (e=3/3 block=0 draw=4 disc=5)

  - hand:
    - [0] `DefendIronclad` cost=1 canPlay=True target=Self
    - [1] `Bloodletting` cost=0 canPlay=True target=Self
    - [2] `Bash` cost=2 canPlay=True target=AnyEnemy
    - [3] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [4] `DefendIronclad` cost=1 canPlay=True target=Self
  - enemies:
    - [0] `PunchConstruct` 37/55 block=10 → Attack 14×1  powers=[ArtifactPower:1]
    - [1] `CubexConstruct` 65/65 block=0 → Attack 7×1 + Buff  powers=[ArtifactPower:1,StrengthPower:2]
    - [2] `CubexConstruct` 65/65 block=0 → Attack 7×1 + Buff  powers=[ArtifactPower:1,StrengthPower:2]
  - player powers: RagePower:3

  → play card [0]
  → play card [0]
  → play card [0] target=0
  → play card [0] target=0
  → play card [0]
  → end_turn → round transition
#### Round 3  (e=3/3 block=0 draw=9 disc=0)

  - hand:
    - [0] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [1] `DefendIronclad` cost=1 canPlay=True target=Self
    - [2] `Debt` cost=0 canPlay=False target=None
    - [3] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [4] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `PunchConstruct` 30/55 block=0 → Attack 5×2 + Debuff
    - [1] `CubexConstruct` 65/65 block=0 → Attack 7×1 + Buff  powers=[ArtifactPower:1,StrengthPower:4]
    - [2] `CubexConstruct` 65/65 block=0 → Attack 7×1 + Buff  powers=[ArtifactPower:1,StrengthPower:4]
  - player powers: RagePower:3

  → play card [0] target=0
  → play card [0]
  → play card [1] target=0
  → end_turn → round transition
#### Round 4  (e=3/3 block=0 draw=4 disc=5)

  - hand:
    - [0] `DefendIronclad` cost=1 canPlay=True target=Self
    - [1] `Bash` cost=2 canPlay=True target=AnyEnemy
    - [2] `Bloodletting` cost=0 canPlay=True target=Self
    - [3] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [4] `DefendIronclad` cost=1 canPlay=True target=Self
  - enemies:
    - [0] `PunchConstruct` 12/55 block=0 → Defend
    - [1] `CubexConstruct` 65/65 block=0 → Attack 5×2  powers=[ArtifactPower:1,StrengthPower:6]
    - [2] `CubexConstruct` 65/65 block=0 → Attack 5×2  powers=[ArtifactPower:1,StrengthPower:6]
  - player powers: RagePower:3, WeakPower:1

  → play card [0]
  → play card [0] target=0
  → play card [0]
  → play card [0] target=0
  → play card [0]
  → end_turn → round transition
  - rewards offered:
    - [0] gold 14g  canSkip=False
    - [1] card cards=[`BattleTrance`(cost=0), `Headbutt`(cost=1), `ShrugItOff`(cost=1)]  canSkip=True

  - rewards offered:
    - [0] gold 14g  canSkip=False
    - [1] card cards=[`BattleTrance`(cost=0), `Headbutt`(cost=1), `ShrugItOff`(cost=1)]  canSkip=True

  - heal → hp=80/80

  - rewards offered:
    - [0] gold 14g  canSkip=False
    - [1] card cards=[`BattleTrance`(cost=0), `Headbutt`(cost=1), `ShrugItOff`(cost=1)]  canSkip=True

  → claim reward [0] → hp=80 room=CombatRoom
  - rewards offered:
    - [0] card cards=[`BattleTrance`(cost=0), `Headbutt`(cost=1), `ShrugItOff`(cost=1)]  canSkip=True

  - rewards offered:
    - [0] card cards=[`BattleTrance`(cost=0), `Headbutt`(cost=1), `ShrugItOff`(cost=1)]  canSkip=True

## Floor 11: MapRoom  (hp=80)

  - map options: (6,11):Unknown, (4,11):Monster

  → skip reward [0] → hp=80 room=MapRoom
  - combat ended (hp=80)

## Floor 12: CombatRoom  (hp=80)


  → pick map (4,11) → CombatRoom floor=12
### Combat #1 on floor 12

#### Round 1  (e=3/3 block=0 draw=9 disc=0)

  - hand:
    - [0] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [1] `DefendIronclad` cost=1 canPlay=True target=Self
    - [2] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [3] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [4] `DefendIronclad` cost=1 canPlay=True target=Self
  - enemies:
    - [0] `Fabricator` 150/150 block=0 → Unknown

  → play card [0] target=0
  → play card [0]
  → play card [0] target=0
  → end_turn → round transition
#### Round 2  (e=3/3 block=0 draw=4 disc=5)

  - hand:
    - [0] `BodySlam` cost=1 canPlay=True target=AnyEnemy
    - [1] `Bash` cost=2 canPlay=True target=AnyEnemy
    - [2] `Debt` cost=0 canPlay=False target=None
    - [3] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [4] `DefendIronclad` cost=1 canPlay=True target=Self
  - enemies:
    - [0] `Noisebot` 23/23 block=0 → Unknown  powers=[MinionPower:1]
    - [1] `Stabbot` 20/20 block=0 → Attack 11×1 + Debuff  powers=[MinionPower:1]
    - [2] `Fabricator` 132/150 block=0 → Unknown

  → play card [0] target=0
  → play card [0] target=0
  → end_turn → round transition
#### Round 3  (e=3/3 block=0 draw=0 disc=11)

  - hand:
    - [0] `Rage` cost=0 canPlay=True target=Self
    - [1] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [2] `Bloodletting` cost=0 canPlay=True target=Self
    - [3] `DefendIronclad` cost=1 canPlay=True target=Self
    - [4] `Dazed` cost=0 canPlay=False target=None
  - enemies:
    - [0] `Noisebot` 15/23 block=0 → Unknown  powers=[MinionPower:1,VulnerablePower:1]
    - [1] `Stabbot` 20/20 block=0 → Attack 11×1 + Debuff  powers=[MinionPower:1]
    - [2] `Fabricator` 132/150 block=0 → Attack 11×1
    - [3] `Noisebot` 22/22 block=0 → Unknown  powers=[MinionPower:1]
    - [4] `Stabbot` 21/21 block=0 → Attack 11×1 + Debuff  powers=[MinionPower:1]
  - player powers: FrailPower:1

  → play card [0]
  → play card [0] target=0
  → play card [0]
  → play card [0]
  → end_turn → round transition
#### Round 4  (e=3/3 block=0 draw=14 disc=0)

  - hand:
    - [0] `Dazed` cost=0 canPlay=False target=None
    - [1] `Dazed` cost=0 canPlay=False target=None
    - [2] `Rage` cost=0 canPlay=True target=Self
    - [3] `Debt` cost=0 canPlay=False target=None
    - [4] `DefendIronclad` cost=1 canPlay=True target=Self
  - enemies:
    - [0] `Noisebot` 2/23 block=0 → Unknown  powers=[MinionPower:1]
    - [1] `Stabbot` 20/20 block=0 → Attack 11×1 + Debuff  powers=[MinionPower:1]
    - [2] `Fabricator` 132/150 block=0 → Attack 11×1
    - [3] `Noisebot` 22/22 block=0 → Unknown  powers=[MinionPower:1]
    - [4] `Stabbot` 21/21 block=0 → Attack 11×1 + Debuff  powers=[MinionPower:1]
  - player powers: FrailPower:3, RagePower:3

  → play card [2]
  → play card [3]
  → end_turn → round transition
#### Round 5  (e=3/3 block=0 draw=11 disc=5)

  - hand:
    - [0] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [1] `BodySlam` cost=1 canPlay=True target=AnyEnemy
    - [2] `Dazed` cost=0 canPlay=False target=None
    - [3] `DefendIronclad` cost=1 canPlay=True target=Self
    - [4] `Dazed` cost=0 canPlay=False target=None
  - enemies:
    - [0] `Noisebot` 2/23 block=0 → Unknown  powers=[MinionPower:1]
    - [1] `Stabbot` 20/20 block=0 → Attack 11×1 + Debuff  powers=[MinionPower:1]
    - [2] `Fabricator` 132/150 block=0 → Attack 11×1
    - [3] `Noisebot` 22/22 block=0 → Unknown  powers=[MinionPower:1]
    - [4] `Stabbot` 21/21 block=0 → Attack 11×1 + Debuff  powers=[MinionPower:1]
  - player powers: FrailPower:5, RagePower:6

  → play card [0] target=0
  → play card [0] target=0
  → play card [1]
  → end_turn → round transition
  - rewards offered:
    - [0] gold 15g  canSkip=False
    - [1] potion potion=`StrengthPotion`  canSkip=False
    - [2] card cards=[`Dominate`(cost=1), `Rage`(cost=0), `BodySlam`(cost=1)]  canSkip=True

  - rewards offered:
    - [0] gold 15g  canSkip=False
    - [1] potion potion=`StrengthPotion`  canSkip=False
    - [2] card cards=[`Dominate`(cost=1), `Rage`(cost=0), `BodySlam`(cost=1)]  canSkip=True

  - heal → hp=80/80

  - rewards offered:
    - [0] gold 15g  canSkip=False
    - [1] potion potion=`StrengthPotion`  canSkip=False
    - [2] card cards=[`Dominate`(cost=1), `Rage`(cost=0), `BodySlam`(cost=1)]  canSkip=True

  → claim reward [0] → hp=80 room=CombatRoom
  - rewards offered:
    - [0] potion potion=`StrengthPotion`  canSkip=False
    - [1] card cards=[`Dominate`(cost=1), `Rage`(cost=0), `BodySlam`(cost=1)]  canSkip=True

  - rewards offered:
    - [0] potion potion=`StrengthPotion`  canSkip=False
    - [1] card cards=[`Dominate`(cost=1), `Rage`(cost=0), `BodySlam`(cost=1)]  canSkip=True

  → claim reward [0] → hp=80 room=CombatRoom
  - rewards offered:
    - [0] card cards=[`Dominate`(cost=1), `Rage`(cost=0), `BodySlam`(cost=1)]  canSkip=True

  - rewards offered:
    - [0] card cards=[`Dominate`(cost=1), `Rage`(cost=0), `BodySlam`(cost=1)]  canSkip=True

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

  → pick rest option [1] → MapRoom hp=80
## Floor 15: BossRoom  (hp=80)


  → pick map (3,14) → BossRoom floor=15
### Combat #1 on floor 15

#### Round 1  (e=3/3 block=0 draw=9 disc=0)

  - hand:
    - [0] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [1] `BodySlam` cost=1 canPlay=True target=AnyEnemy
    - [2] `DefendIronclad` cost=1 canPlay=True target=Self
    - [3] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [4] `Rage` cost=0 canPlay=True target=Self
  - enemies:
    - [0] `TestSubject` 100/100 block=0 → Attack 20×1  powers=[AdaptablePower:1,EnragePower:2]

  → play card [0] target=0
  → play card [0] target=0
  → play card [0]
  → play card [1]
  → end_turn → round transition
#### Round 2  (e=3/3 block=0 draw=4 disc=5)

  - hand:
    - [0] `DefendIronclad` cost=1 canPlay=True target=Self
    - [1] `DefendIronclad` cost=1 canPlay=True target=Self
    - [2] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [3] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [4] `Bloodletting` cost=0 canPlay=True target=Self
  - enemies:
    - [0] `TestSubject` 91/100 block=0 → Attack 14×1 + Debuff  powers=[AdaptablePower:1,EnragePower:2,StrengthPower:4]
  - player powers: RagePower:3

  → play card [0]
  → play card [0]
  → play card [0] target=0
  → play card [1]
  → play card [0] target=0
  → end_turn → round transition
#### Round 3  (e=3/3 block=0 draw=9 disc=0)

  - hand:
    - [0] `Debt` cost=0 canPlay=False target=None
    - [1] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [2] `Bash` cost=2 canPlay=True target=AnyEnemy
    - [3] `DefendIronclad` cost=1 canPlay=True target=Self
    - [4] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
  - enemies:
    - [0] `TestSubject` 73/100 block=0 → Attack 20×1  powers=[AdaptablePower:1,EnragePower:2,StrengthPower:10]
  - player powers: RagePower:3, VulnerablePower:1

  → play card [1] target=0
  → play card [1] target=0
  → end_turn → round transition
#### Round 4  (e=3/3 block=0 draw=4 disc=5)

  - hand:
    - [0] `DefendIronclad` cost=1 canPlay=True target=Self
    - [1] `BodySlam` cost=1 canPlay=True target=AnyEnemy
    - [2] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [3] `StrikeIronclad` cost=1 canPlay=True target=AnyEnemy
    - [4] `Rage` cost=0 canPlay=True target=Self
  - enemies:
    - [0] `TestSubject` 56/100 block=0 → Attack 14×1 + Debuff  powers=[AdaptablePower:1,EnragePower:2,StrengthPower:10,VulnerablePower:1]
  - player powers: RagePower:3, VulnerablePower:1

  → play card [0]
  → play card [0] target=0
  → play card [0] target=0
  → play card [1]
## Floor 15: MapRoom  (hp=0)


  → end_turn → round transition
  - combat ended (hp=0)

  - heal → hp=80/80

## Floor 15: EventRoom  (hp=80)

  - event options:
    - [0] `THE_ARCHITECT.dialogue.0`

  → enter_next_act → EventRoom floor=15
  → pick event option [0] → EventRoom hp=80
## Floor 16: EventRoom  (hp=0)

  - event options:
    - [0] `PROCEED`

  → pick event option [0] → EventRoom hp=0
  - heal → hp=80/80

## Floor 17: EventRoom  (hp=0)

  - event options:
    - [0] `PROCEED`

  → pick event option [0] → EventRoom hp=0
  - heal → hp=80/80

## Floor 18: EventRoom  (hp=0)

  - event options:
    - [0] `PROCEED`

  → pick event option [0] → EventRoom hp=0
  - heal → hp=80/80

## Floor 19: EventRoom  (hp=0)

  - event options:
    - [0] `PROCEED`

  → pick event option [0] → EventRoom hp=0
  - heal → hp=80/80

## Floor 20: EventRoom  (hp=0)

  - event options:
    - [0] `PROCEED`

  → pick event option [0] → EventRoom hp=0
  - heal → hp=80/80

## Floor 21: EventRoom  (hp=0)

  - event options:
    - [0] `PROCEED`

  → pick event option [0] → EventRoom hp=0
  - heal → hp=80/80

## Floor 22: EventRoom  (hp=0)

  - event options:
    - [0] `PROCEED`

  → pick event option [0] → EventRoom hp=0
  - heal → hp=80/80

## Floor 23: EventRoom  (hp=0)

  - event options:
    - [0] `PROCEED`

  → pick event option [0] → EventRoom hp=0
  - heal → hp=80/80

## Floor 24: EventRoom  (hp=0)

  - event options:
    - [0] `PROCEED`

  → pick event option [0] → EventRoom hp=0
  - heal → hp=80/80

## Floor 25: EventRoom  (hp=0)

  - event options:
    - [0] `PROCEED`

  → pick event option [0] → EventRoom hp=0
  - heal → hp=80/80

## Floor 26: EventRoom  (hp=0)

  - event options:
    - [0] `PROCEED`

  → pick event option [0] → EventRoom hp=0
  - heal → hp=80/80

## Floor 27: EventRoom  (hp=0)

  - event options:
    - [0] `PROCEED`

  → pick event option [0] → EventRoom hp=0
  - heal → hp=80/80

## Floor 28: EventRoom  (hp=0)

  - event options:
    - [0] `PROCEED`

  → pick event option [0] → EventRoom hp=0
  - heal → hp=80/80

## Floor 29: EventRoom  (hp=0)

  - event options:
    - [0] `PROCEED`

  → pick event option [0] → EventRoom hp=0
  - heal → hp=80/80

## Floor 30: EventRoom  (hp=0)

  - event options:
    - [0] `PROCEED`

  → pick event option [0] → EventRoom hp=0
  - heal → hp=80/80

## Floor 31: EventRoom  (hp=0)

  - event options:
    - [0] `PROCEED`

  → pick event option [0] → EventRoom hp=0
  - heal → hp=80/80

## Floor 32: EventRoom  (hp=0)

  - event options:
    - [0] `PROCEED`

  → pick event option [0] → EventRoom hp=0
  - heal → hp=80/80

## Floor 33: EventRoom  (hp=0)

  - event options:
    - [0] `PROCEED`

  → pick event option [0] → EventRoom hp=0
  - heal → hp=80/80

## Floor 34: EventRoom  (hp=0)

  - event options:
    - [0] `PROCEED`

  → pick event option [0] → EventRoom hp=0
  - heal → hp=80/80

## Floor 35: EventRoom  (hp=0)

  - event options:
    - [0] `PROCEED`

  → pick event option [0] → EventRoom hp=0
  - heal → hp=80/80

## Floor 36: EventRoom  (hp=0)

  - event options:
    - [0] `PROCEED`

  → pick event option [0] → EventRoom hp=0
  - heal → hp=80/80

## Floor 37: EventRoom  (hp=0)

  - event options:
    - [0] `PROCEED`

  → pick event option [0] → EventRoom hp=0
  - heal → hp=80/80

## Floor 38: EventRoom  (hp=0)

  - event options:
    - [0] `PROCEED`

  → pick event option [0] → EventRoom hp=0
  - heal → hp=80/80

## Floor 39: EventRoom  (hp=0)

  - event options:
    - [0] `PROCEED`

  → pick event option [0] → EventRoom hp=0
  - heal → hp=80/80

