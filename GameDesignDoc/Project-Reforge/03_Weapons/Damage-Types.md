

## Purpose

Damage Types define how damage interacts with enemy [[Combat-Glossary#Shield]] and [[Combat-Glossary#Armor]].

Damage Type is independent from [[Weapon-Types]] and [[Weapon-Effects]].

---

## Damage Types

| Damage Type | Shield Damage | Armor Damage |
| ----------- | ------------: | -----------: |
| Kinetic     |           75% |         125% |
| Explosion   |           90% |         110% |
| Chemical    |        Bypass |         100% |
| Plasma      |          125% |          75% |
| Microwave   |          110% |          90% |
| Electric    |          110% |          90% |

---

## Kinetic

Physical damage caused by bullets, shells, blades, fragments, and other high-speed objects.

- **Shield:** 75%
    
- **Armor:** 125%
    

---

## Explosion

Damage caused by explosive force and fragmentation.

- **Shield:** 90%
    
- **Armor:** 110%
    

---

## Chemical

Damage caused by acids, corrosive substances, and other chemical agents.

Chemical damage **bypasses Shield** and is applied directly to Armor.

- **Shield:** Bypassed
    
- **Armor:** 100%
    

Example:

```text
Enemy:
Shield: 400
Armor: 200

Chemical attack: 50 damage

Result:
Shield: 400
Armor: 150
```

---

## Plasma

Damage caused by highly energized plasma.

- **Shield:** 125%
    
- **Armor:** 75%
    

---

## Microwave

Damage caused by focused microwave radiation.

- **Shield:** 110%
    
- **Armor:** 90%
    

---

## Electric

Damage caused by electrical discharge.

- **Shield:** 110%
    
- **Armor:** 90%
    

---

## Damage Calculation

For normal Damage Types:

```text
Final Damage = Base Damage × Damage Modifier
```

Example:

```text
Kinetic Damage: 100

Against Shield:
100 × 0.75 = 75

Against Armor:
100 × 1.25 = 125
```

Chemical damage ignores Shield and applies its damage directly to Armor.

---

## Related Documents

- [[Weapons-Definition]]
    
- [[Weapon-Types]]
    
- [[Weapon-Effects]]
    
- [[Combat-Glossary]]