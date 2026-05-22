# Vehicle Tracker — Project Brief

## Tagline

> *"Wiesz kiedy wymienić olej. Nie wiesz kiedy wymienić opony. My to wiemy za Ciebie."*

---

## Problem

Właściciel samochodu lub motocykla nie ma jednego miejsca, gdzie może zobaczyć **co wymaga uwagi w jego pojeździe i kiedy**. Historia serwisów żyje w głowie, na paragonach w schowku albo u mechanika. Terminy przeglądów mijają, bo nikt nie pilnuje. Opony mają 7 lat, bo nikt nie sprawdzał daty DOT. Polisa OC wygasa, bo przypomnienie nie dotarło.

**Konkretne bóle:**

- "Kiedy był ostatni olej i ile km temu?"
- "Serwisant powiedział że opony są stare — nie wiedziałem że mają datę ważności"
- "Polisa wygasła tydzień temu, bo nie pamiętałem"
- "Ile mniej więcej zapłacę za następny serwis?"

---

## Użytkownik

Tomasz, właściciel 1–2 pojazdów (auto + motocykl). Regularnie serwisuje, ale nie pamięta co i kiedy. Nie jest mechanikiem — chce prostego narzędzia, które powie mu co zrobić, zanim będzie za późno.

---

## Logika domenowa — jedno zdanie

> *"Dla każdej pozycji serwisowej system stosuje właściwy typ logiki: porównuje ostatni wpis z bieżącym przebiegiem i datą (interwаł km/czas), albo porównuje zmierzoną wartość z progiem (pomiar), i klasyfikuje status: Zaległe / Zbliża się / OK."*

Cztery typy logiki alertów:

| Typ | Trigger | Przykład |
|---|---|---|
| Tylko czas | minął X miesięcy od ostatniego serwisu | Płyn hamulcowy, akumulator, wiek opony (DOT) |
| Tylko km | przejechano X km od ostatniego serwisu | Filtr powietrza |
| Km LUB czas — cokolwiek pierwsze | którykolwiek warunek spełniony | Olej silnikowy, rozrząd |
| Pomiar (próg) | zmierzona wartość < próg | Bieżnik opon (mm), klocki hamulcowe (mm) |

---

## MVP — zakres (tydzień pracy po godzinach)

### Pojazdy

- Dodaj pojazd: marka, model, rok produkcji, numer rejestracyjny, aktualny przebieg
- Przebieg aktualizowalny ręcznie w dowolnym momencie (pole na dashboardzie) + system pyta o przebieg przy każdym nowym wpisie serwisowym
- Pola techniczne decydujące o katalogu serwisowym:
  - **Napęd:** FWD / RWD / AWD
  - **Typ skrzyni:** manualna / S-tronic (DCT mokra) / automat (konwencjonalny) / CVT
  - **Typ paliwa:** benzyna (TFSI/TSI) / diesel (TDI/CDI) / hybryda MHEV / hybryda PHEV / elektryczny
- Wiele pojazdów per konto
- Edycja i usunięcie pojazdu

> **Pilot:** Audi A4 45 TFSI quattro ultra (204 KM), skrzynia S-tronic 7-biegowa, napęd AWD, benzyna — 2024

### Ubezpieczenia OC/AC

- Dodaj polisę: typ (OC/AC/OC+AC), ubezpieczyciel, data początku, data końca, składka roczna
- Przypomnienie 30 i 7 dni przed wygaśnięciem — wyświetlane w aplikacji (dashboard / widok alertów), bez email w MVP
- Historia polis per pojazd

### Opony

- Dodaj komplet opon: data produkcji (rok z kodu DOT), rozmiar, marka, bieżnik per opona (mm), typ kompletu (letnie / zimowe / całoroczne)
- Opcjonalne śledzenie przebiegu per komplet — użytkownik podaje km przy zakładaniu i zdejmowaniu; pozwala ocenić zużycie bieżnika przez km, nie tylko wiek
- Automatyczne wyliczenie wieku opony w latach
- Status wieku: 🟢 < 4 lata / 🟡 4–6 lat / 🔴 > 6 lat
- Status bieżnika: 🟢 ≥ 4 mm / 🟡 2–4 mm / 🔴 < 2 mm (granica prawna)
- **Alert AWD:** jeśli pojazd ma napęd AWD i różnica bieżnika między oponami > 2 mm → 🔴 alert "Wyrównaj bieżniki — ryzyko uszkodzenia skrzyni AWD (Quattro / xDrive / Haldex)"
- Możliwość oznaczenia "wymienione" (archiwizacja starego kompletu)

### Badania techniczne (przegląd rejestracyjny)

- Data ostatniego badania i data ważności
- Przypomnienie 30 i 7 dni przed upływem terminu — wyświetlane w aplikacji (dashboard / widok alertów), bez email w MVP
- Historia badań

### Serwisy — granularne pozycje

Podejście: **każda pozycja serwisowa osobno** z własnym interwałem i historią wpisów. Dzięki temu wiadomo co i kiedy było robione, a nie tylko "był jakiś serwis".

**Predefiniowany katalog pozycji (seed data):**

Pozycje bazowe — aktywne dla każdego pojazdu:

| Pozycja | Typ logiki | Domyślny interwał / próg |
|---|---|---|
| Olej silnikowy | km LUB czas | 15 000 km / 12 mies |
| Filtr oleju | km LUB czas | 15 000 km / 12 mies |
| Filtr kabinowy | km LUB czas | 15 000 km / 12 mies |
| Filtr powietrza | tylko km | 30 000 km |
| Płyn hamulcowy | tylko czas | 24 mies |
| Płyn chłodniczy | tylko czas | 36 mies |
| Rozrząd / łańcuch | km LUB czas | 120 000 km / 8 lat |
| Akumulator | tylko czas | 48–60 mies |
| Klocki hamulcowe — przód | pomiar (próg) | 🔴 < 3 mm / 🟡 3–5 mm |
| Klocki hamulcowe — tył | pomiar (próg) | 🔴 < 3 mm / 🟡 3–5 mm |

Pozycje warunkowe — aktywowane na podstawie pól technicznych pojazdu:

| Pozycja | Warunek | Typ logiki | Domyślny interwał / próg |
|---|---|---|---|
| Świece zapłonowe | benzyna | tylko km | 60 000 km (iridium) |
| Filtr paliwa | diesel | tylko km | 60 000 km |
| AdBlue — dolewka | diesel | pomiar (próg) | 🔴 < 2 400 km rezerwy |
| Olej S-tronic / DSG | S-tronic | km LUB czas | 60 000 km / 6 lat |
| Olej skrzyni automat | automat konw. | tylko km | 60 000 km |
| Olej Haldex / Quattro ultra | AWD | km LUB czas | 40 000 km / 4 lata |

> **Dla pilota (Audi A4 TFSI S-tronic AWD)** system aktywuje: świece, olej S-tronic, olej Haldex. Nie aktywuje: filtra paliwa, AdBlue.

**Interwały per pozycja:**
- Interwał OEM (producent) — informacyjnie
- Interwał "mój" — edytowalny przez użytkownika (np. wolę olej co 15k zamiast 30k)
- System liczy alerty na podstawie "mojego" interwału

**Historia wpisów serwisowych:**
- Data, przebieg przy serwisie, koszt, warsztat, notatki
- Na podstawie ostatniego wpisu system oblicza kiedy następny

**Dashboard alertów:**
- 🔴 Zaległe — przekroczony km lub czas (interwał) / wartość poniżej progu krytycznego (pomiar)
- 🟡 Zbliża się — < 1 000 km lub < 30 dni (interwał) / wartość w strefie ostrzeżenia (pomiar)
- 🟢 OK
- Widok zbiorczy: "co mnie czeka w ciągu 90 dni"

### Konto i dostęp

- Rejestracja / logowanie (email + hasło)
- Każdy użytkownik widzi tylko swoje pojazdy

---

## Czego MVP nie robi

| Wykluczone | Powód |
|---|---|
| Integracja z API producentów (OEM schedules) | brak darmowych API dla aut europejskich |
| Integracja z NHTSA / VIN decode | sprint 2 — nie blokuje MVP |
| Załączniki (zdjęcia, faktury PDF) | sprint 2 |
| Szacowanie kosztów serwisu | sprint 2 |
| Asystent AI — analiza danych pojazdu i doradztwo ("co wymienić i dlaczego") | sprint 2 — Azure OpenAI + kontekst danych z bazy |
| Powiadomienia email (OC/AC, badania) | sprint 2 — SendGrid free tier (100 maili/dzień) |
| PWA / powiadomienia push | sprint 2 |
| Raport PDF do sprzedaży auta | sprint 3 |
| Śledzenie paliwa i spalania | Drivvo to robi, nie jest rdzeniem wartości |
| GPS / śledzenie tras | inna aplikacja |
| OBD2 / diagnostyka | wymaga sprzętu |
| Rezerwacja wizyty w warsztacie | za duża złożoność |
| Aplikacja mobilna natywna | PWA wystarczy na start |
| Zarządzanie flotą firmową | inny segment, osobny produkt |

---

## Przepływ użytkownika — tydzień 1 (must work)

```
Rejestracja → Dodaj pojazd → Dodaj pozycję serwisową →
Dodaj wpis historii (data, km, co zrobiono) →
Dashboard: pozycja zmienia status na 🟢 OK →
Przesuń bieżący przebieg o +15 000 km →
Dashboard: pozycja zmienia status na 🔴 Zaległe
```

To jest test E2E dla certyfikacji.
