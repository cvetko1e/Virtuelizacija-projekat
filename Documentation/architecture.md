# Arhitektura sistema i pravila protokola

```
Client Console App
        |
        | StartSession(meta)
        | PushSample(sample) - jedan po jedan CSV red kroz for petlju
        | EndSession()
        v
WCF Service - WeatherService
        |
        | ACK/NACK + IN_PROGRESS/COMPLETED/FAILED
        v
Storage na disku
        |
        | measurements_session.csv
        | rejects.csv
```

## Komponente

- Client
  - Konzolna aplikacija koja cita CSV fajl i sekvencijalno salje uzorke servisu.
  - Poziva operacije `StartSession`, `PushSample` i `EndSession`.
- WCF Service
  - Implementacija `WeatherService` koja prima uzorke, validira podatke i vraca rezultat obrade.
  - Za svaki poslati uzorak vraca odgovor sa ACK/NACK i statusom obrade.
- Storage na disku
  - Prihvaceni uzorci se upisuju u `measurements_session.csv`.
  - Odbijeni uzorci i razlozi odbijanja se upisuju u `rejects.csv`.
- Common biblioteka
  - Sadrzi ugovore servisa, modele podataka, fault tipove i zajednicke enum vrednosti.

## Pravila protokola

1. Svaka sesija pocinje pozivom `StartSession(meta)`.
2. Meta-zaglavlje sadrzi polja: `{ T, Tpot, Tdew, Sh, Rh, Date }`.
3. Klijent cita CSV i sekvencijalno, kroz for petlju, salje jedan po jedan red.
4. Svaki red se salje metodom `PushSample(sample)`.
5. Sesija se zavrsava metodom `EndSession()`.
6. Server vraca ACK ako je uzorak prihvacen.
7. Server vraca NACK ako je uzorak odbijen, uz razlog odbijanja.
8. Status moze biti `IN_PROGRESS`, `COMPLETED` ili `FAILED`.
9. Pragovi se citaju iz `App.config`:
   - `HI_max_threshold`
   - `SH_threshold`
   - `OUT_OF_BAND_PERCENT = 25`

## Veza sa Kontrolnom tačkom 1

- Zadatak 1 pokriven kroz `Documentation/architecture.md` i `README.md`.
- Zadatak 2 pokriven kroz `Common/IWeatherService.cs`, `Common/WeatherSample.cs`, `Service/App.config` i `Client/App.config`.
- Zadatak 3 pokriven kroz `Service/WeatherService.cs` i fault klase u `Common` projektu.
- Zadatak 4 pokriven kroz `Service/WeatherStorage.cs` i `Client/CsvWeatherReader.cs`.
- Zadatak 5 pokriven kroz `Client/CsvWeatherReader.cs` i `Client/Program.cs`.
