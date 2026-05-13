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

- Preciznija formula za Heat Index prema materijalu.
- Dodatna validacija i robustniji fault handling.
- Bolje logovanje i test primeri CSV dataset-a.
- Event pretplatnici i dodatna obrada warning-a.
