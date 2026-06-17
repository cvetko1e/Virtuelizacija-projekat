# Virtuelizacija procesa - WCF Template

Tema: **Simulacija i razmena podataka meteorološke stanice koriscenjem WCF servisa i dogadjajnog modela**

## Struktura solution-a

- `Common` - zajednicki ugovori i modeli (`ServiceContract`, `DataContract`, `fault`, `enum`, `EventArgs`).
- `Service` - konzolni host koji podize WCF servis preko `ServiceHost` i cuva podatke u fajlove.
- `Client` - konzolni klijent koji preko `ChannelFactory` cita CSV i salje uzorke sekvencijalno.

## Uloga projekata

- **Common**
  - Definise `IWeatherService` ugovor.
  - Definise `WeatherSample`, `SessionMeta`, `TransferResponse`, `TransferStatus`.
  - Definise `DataFormatFault`, `ValidationFault` i event args klase.

- **Service**
  - Hostuje `WeatherService` na `net.tcp://localhost:4000/WeatherService`.
  - Ima osnovne event-e za transfer i warning-e.
  - Koristi `WeatherStorage` za `measurements_session.csv` i `rejects.csv`.
  - Ima osnovni `Dispose` pattern i jednostavnu analitiku (`WeatherAnalytics`).

- **Client**
  - Cita prvih 113 validnih redova iz CSV-a (`CsvWeatherReader`).
  - Poziva `StartSession`, `PushSample`, `EndSession`.
  - Ispisuje ACK/NACK i status.

## Pokretanje

1. Pokrenuti `Service` projekat.
2. Pokrenuti `Client` projekat (opciono proslediti putanju do CSV fajla kao argument).

## Kontrolne tacke koje pokriva template

- WCF struktura `Common/Service/Client` (V1_WCF).
- Klasican `App.config`, `ServiceHost`, `ChannelFactory`.
- Rad sa fajlovima (`Directory`, `StreamReader`, `StreamWriter`) (V4).
- Pocetni `Dispose` pattern (V3).
- Dogadjaji i delegati u servisu (V6, V7).
- Priprema za mrezni prenos podataka i fajlova (V5, V8 stil jednostavne servisne organizacije).

## Sta dopuniti kasnije

- ~~Preciznija formula za Heat Index prema materijalu.~~ ✅ Implementirano (Rothfusz regresija + NWS korekcije).
- ~~Dodatna validacija i robustniji fault handling.~~ ✅ Implementirano (DataFormatFault/ValidationFault sa Field/Code, prosireni opsezi).
- ~~Bolje logovanje i test primeri CSV dataset-a.~~ ✅ Implementirano (Logger klasa sa fajl+konzola izlazom, test_dataset.csv sa validnim/nevalidnim redovima).
- ~~Event pretplatnici i dodatna obrada warning-a.~~ ✅ Implementirano (WeatherEventHandler sa pretplatom, klasifikacijom ozbiljnosti i sumarnim izvestajem).


## Kontrolna tacka 1 - pokrivenost

1. Skica sistema i pravila protokola
   - Dokumentacija: Documentation/architecture.md
   - Protokol: StartSession, PushSample, EndSession
   - ACK/NACK i statusi: TransferResponse, TransferStatus

2. WCF servis, konfiguracija i ugovori
   - Common/IWeatherService.cs
   - Common/WeatherSample.cs
   - Service/App.config
   - Client/App.config

3. WCF operacije i validacija podataka
   - Service/WeatherService.cs
   - Common/DataFormatFault.cs
   - Common/ValidationFault.cs
   - meta-zaglavlje se proverava kao tacan niz: T,Tpot,Tdew,Sh,Rh,Date
   - format greske bacaju DataFormatFault, a opsezi/jedinice ValidationFault

4. Dispose pattern i upravljanje resursima
   - Service/WeatherStorage.cs
   - Client/CsvWeatherReader.cs
   - simulacija prekida prenosa: Service.exe --simulate-dispose

5. Rad sa fajlovima i ucitavanje CSV-a na klijentu
   - Client/CsvWeatherReader.cs
   - Client/Program.cs
   - ucitavanje prvih 113 validnih redova
   - ceo dataset se procita, a nevalidni i visak redovi idu u izdvojeni csv_issues_*.csv log

## Kontrolna tacka 2 - pokrivenost

6. Snimanje i organizacija fajlova na serveru
   - Service/WeatherStorage.cs
   - StartSession pravi poseban folder sesije
   - measurements_session.csv cuva prihvacena merenja
   - rejects.csv cuva odbacena merenja sa razlogom

7. Mrezni prenos i tokovi, sekvencijalni streaming
   - Client/Program.cs salje uzorke jedan po jedan kroz for petlju
   - Service/WeatherService.cs prima svaki uzorak kroz PushSample
   - Service/WeatherEventHandler.cs ispisuje statuse "Prenos u toku..." i "Zavrsen prenos."
