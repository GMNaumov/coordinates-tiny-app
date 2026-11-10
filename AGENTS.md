# Контекст проекта

## Цель

Разрабатывается standalone desktop-приложение для управления данными об изделии (PDM) на C# с использованием WPF. Текущая функциональность посвящена загрузке, отображению, редактированию, сохранению и сравнению координат объектов, представленных матрицей преобразования 4 × 4.

Пример входного блока:

```xml
<TransformationMap m11="1" m12="-0" m13="0" m14="-0" m21="-0" m22="1" m23="-0" m24="0" m31="0" m32="-0" m33="1" m34="-0" m41="-0.514" m42="0.11" m43="0.17200000000000001" m44="1"/>
```

## Ограничения и принятые решения

- Целевые ОС: Windows 10 и Windows 11. Поддержка Linux не требуется.
- Платформа: .NET 8 LTS, целевой монiker `net8.0-windows`.
- UI: WPF.
- Архитектура интерфейса: MVVM без стороннего MVVM-фреймворка. Состояние и команды находятся в `ViewModels`, XAML использует привязки; code-behind отвечает только за специфичные для WPF действия и связывание компонентов.
- Использовать преимущественно стандартные библиотеки .NET. Third-party компоненты не применять; популярные NuGet-пакеты допустимы только при реальной необходимости.
- Локальный .NET SDK не установлен. Для восстановления пакетов, сборки и других команд .NET использовать Docker Desktop и образ `mcr.microsoft.com/dotnet/sdk:8.0`.
- Код структурировать и комментировать основные блоки для поддерживаемости, не добавляя очевидных комментариев.
- Стандарт кодирования: `naming-conventions.md`. Обязательно перечитывать его при необходимости и соблюдать во всех новых и изменяемых файлах.
- Поддержка запуска без предустановленного .NET Runtime (self-contained публикация) будет настроена на этапе реализации публикации, если это потребуется.
- API и запись в базу данных пока не требуются. Поддерживается чтение координат из PostgreSQL через Npgsql 8.0.9 (совместим с .NET 8); для DPAPI используется официальный пакет System.Security.Cryptography.ProtectedData 8.0.0.

## Реализованная функциональность

- На вкладке «Редактор матрицы» за один раз обрабатывается ровно один блок `TransformationMap`.
- Исходный XML можно вставить строкой или загрузить из файла `.txt`/`.xml` размером до 64 КБ.
- Корневой элемент должен называться `TransformationMap`, находиться без пространства имён и содержать ровно 16 обязательных атрибутов от `m11` до `m44`; вложенные элементы, текст и дополнительные атрибуты запрещены.
- Значения XML читаются как конечные `double` в инвариантной культуре; `NaN`, бесконечности и числа с запятой в исходном XML отклоняются. DTD запрещены, размер XML-документа ограничен.
- В редакторе допускаются десятичный разделитель инвариантной и текущей культуры; разделители групп разрядов не принимаются.
- Все 16 элементов отображаются в редактируемой матрице 4 × 4. Значения `m41`, `m42`, `m43` дополнительно показываются как смещение X, Y, Z.
- Изменения можно применить или сбросить к последней загруженной/применённой матрице.
- Сохранение выполняется в `.xml` в формате исходного `TransformationMap`, в UTF-8 без BOM и с числами в инвариантной культуре. Для записи используется round-trip формат `double`.
- Сериализатор проверяет созданный XML повторным разбором; после записи файл также повторно читается и проверяется.
- На отдельной вкладке «Сравнение матриц» можно вставить два блока `TransformationMap` или загрузить каждый из файла `.txt`/`.xml` до 64 КБ с теми же правилами проверки XML.
- Сравнение выполняется по всем 16 значениям `double` точно, без допуска; различия в записи чисел и знаке нуля не учитываются. Результат — сообщение об идентичности либо таблица отличающихся параметров с первым, вторым значениями и разностью «вторая − первая». Числа в таблице отображаются в round-trip формате; переполнение разности обозначается явно.
- При изменении любого исходного блока результат сравнения сбрасывается. Ошибки сравнения отображаются на той же вкладке с указанием первой или второй матрицы.
- В редакторе и для каждого входа сравнения доступна загрузка из PostgreSQL. Приложение читает `dbo."Coordinates"`, поля `"s_ObjectID"` (integer), `"s_Guid"` (uuid), `"Placement"` (text); регистр имён сохраняется кавычками SQL. Запросы приложения только читают данные.
- Окно выбора показывает колонки «ID объекта справочника», «GUID объекта», «Матрица трансформации», поддерживает страницы по 100 записей и точный поиск по ID либо GUID. Для применения поиска нужно нажать «Найти / обновить»; пустой поиск показывает все записи. NULL и некорректный XML не загружаются; размер Placement ограничен 64 КБ в UTF-8 на сервере до передачи клиенту, выбранный XML проверяется общим сериализатором.
- Общие настройки PostgreSQL (сервер, порт, база, логин, пароль) доступны через кнопку над вкладками и из окна выбора. Хранятся в `%LOCALAPPDATA%\CoordinatesTinyApp\database-settings.json`, запись заменяет файл после полного сохранения. Пароль сохраняется только как DPAPI CurrentUser ciphertext в Base64, без ключа в приложении; для другого пользователя Windows/компьютера нужно повторно ввести настройки. Повреждённый файл или ошибка расшифровки отображаются пользователю; повторное сохранение позволяет заменить настройки.
- Проверка подключения выполняет `SELECT 1` и не требует наличия таблицы. Сохранение настроек не требует доступного сервера. Подключение и загрузка асинхронны; тайм-аут подключения 10 секунд, команды 15 секунд, общий срок операции 30 секунд; закрытие диалога отменяет запрос. Строки подключения и исходные сообщения сервера не выводятся пользователю. Используется стандартный для Npgsql режим SSL Prefer, без отдельной настройки TLS в интерфейсе.

## Текущая структура

```text
CoordinatesTinyApp.sln
src/
└── CoordinatesTinyApp/
    ├── CoordinatesTinyApp.csproj
    ├── App.xaml
    ├── App.xaml.cs
    ├── Assets/
    │   └── coordinates.ico
    ├── MainWindow.xaml
    ├── MainWindow.xaml.cs
    ├── Infrastructure/
    │   ├── AsyncRelayCommand.cs
    │   ├── ObservableObject.cs
    │   └── RelayCommand.cs
    ├── Models/
    │   ├── CoordinateRecord.cs
    │   ├── DatabaseConnectionSettings.cs
    │   ├── TransformationMap.cs
    │   └── TransformationMapDifference.cs
    ├── Services/
    │   ├── DatabaseErrorMessages.cs
    │   ├── DatabaseSettingsStore.cs
    │   ├── ICoordinateDatabase.cs
    │   ├── IDatabaseSettingsStore.cs
    │   ├── IPasswordProtector.cs
    │   ├── IUserDialogService.cs
    │   ├── PostgresCoordinateDatabase.cs
    │   ├── PostgresCoordinateReader.cs
    │   ├── TransformationMapComparer.cs
    │   ├── TransformationMapFileReader.cs
    │   ├── TransformationMapXmlSerializer.cs
    │   └── WindowsPasswordProtector.cs
    ├── ViewModels/
    │   ├── DatabaseRecordPickerViewModel.cs
    │   ├── DatabaseSettingsViewModel.cs
    │   ├── MainWindowViewModel.cs
    │   ├── MatrixCellViewModel.cs
    │   ├── MatrixComparisonViewModel.cs
    │   ├── MatrixEditorViewModel.cs
    │   └── StatusViewModel.cs
    └── Views/
        ├── DatabaseRecordPickerWindow.xaml
        ├── DatabaseRecordPickerWindow.xaml.cs
        ├── DatabaseSettingsWindow.xaml
        ├── DatabaseSettingsWindow.xaml.cs
        ├── MatrixEditorView.xaml
        ├── MatrixEditorView.xaml.cs
        ├── MatrixComparisonView.xaml
        ├── MatrixComparisonView.xaml.cs
        └── WindowDialogService.cs
tests/
├── CoordinatesTinyApp.Checks/
│   ├── CoordinatesTinyApp.Checks.csproj
│   ├── DatabaseSettingsChecks.cs
│   ├── PostgresIntegrationChecks.cs
│   ├── Program.cs
│   └── ViewModelChecks.cs
├── CoordinatesTinyApp.WpfChecks/
│   ├── CoordinatesTinyApp.WpfChecks.csproj
│   └── Program.cs
└── Invoke-PostgresChecks.ps1
```

- `CoordinatesTinyApp.csproj` содержит базовые настройки WPF, nullable reference types, implicit usings и `EnableWindowsTargeting` для сборки в Linux-контейнере. Зависимости: Npgsql и официальный пакет DPAPI; ORM и сторонних UI-компонентов нет.
- `Assets/coordinates.ico` является многоразмерной иконкой исполняемого файла и главного окна; исходный SVG после преобразования удалён.
- `App` является стандартной точкой входа WPF и содержит общие стили интерфейса обеих вкладок.
- `MainWindow` содержит вкладки редактора и сравнения. `MainWindowViewModel` создаёт независимые ViewModel обеих вкладок и команду настроек. `MatrixEditorView` содержит XAML редактора, `MatrixEditorViewModel` — загрузку, редактирование, применение/сброс и сохранение; `MatrixCellViewModel` хранит текст отдельной ячейки. Смещения показывают последнюю загруженную/применённую/сохранённую матрицу, как и до рефакторинга.
- `ObservableObject` реализует уведомления свойств; `RelayCommand` и `AsyncRelayCommand` реализуют `ICommand` без зависимости от WPF. Асинхронная команда запрещает повторный запуск до завершения; ожидаемые ошибки обрабатывает ViewModel. `StatusViewModel` предоставляет текст и цвет строки состояния.
- `TransformationMap` хранит независимую копию значений матрицы и предоставляет смещение X/Y/Z.
- `TransformationMapDifference` хранит имя отличающегося параметра, оба значения и разность; `null` в разности означает переполнение `double`.
- `TransformationMapComparer` сравнивает все элементы двух матриц и возвращает отличия в порядке строк матрицы.
- `TransformationMapFileReader` обеспечивает общую проверку расширения и размера загружаемых файлов обеих вкладок.
- `TransformationMapXmlSerializer` отвечает за строгий безопасный разбор и сериализацию XML.
- `MatrixComparisonView` содержит интерфейс сравнения; `MatrixComparisonViewModel` управляет входными данными, загрузкой, проверками и результатом. Представления редактора и сравнения обрабатывают только запросы фокуса при ошибках.
- `DatabaseSettingsStore` реализует `IDatabaseSettingsStore` и отделяет JSON и атомарную замену файла от шифрования через `IPasswordProtector`; рабочая реализация `WindowsPasswordProtector` использует DPAPI. Windows-метод `CreateDefault` создаёт хранилище по стандартному пути. `PostgresCoordinateReader` формирует строку подключения через NpgsqlConnectionStringBuilder и выполняет параметризованные запросы с ограничением выборки. `PostgresCoordinateDatabase` предоставляет эти операции ViewModel через `ICoordinateDatabase`. `DatabaseErrorMessages` возвращает сообщения без секретов.
- `IUserDialogService` отделяет выбор файлов, запись из БД, настройки и предупреждения от ViewModel; `WindowDialogService` реализует его через стандартные WPF-диалоги с владельцем окна. `DatabaseSettingsViewModel` и `DatabaseRecordPickerViewModel` управляют валидацией, асинхронными операциями, отменой, поиском и страницами. Code-behind диалогов связывает жизненный цикл окна с ViewModel, передаёт пароль из `PasswordBox` без dependency property и проверяет, что двойной щелчок пришёлся на строку таблицы.
- `CoordinatesTinyApp.Checks` — консольный проект проверок на `net8.0`, включённый в решение; использует те же пакеты Npgsql и DPAPI. Компилирует исходные файлы моделей, сервисов, инфраструктуры и ViewModel по ссылке для запуска проверок без WPF. Проверяет сравнение, граничные числовые значения, ограничения XML/файлов, точность сериализации, настройки, валидацию выбора и безопасные сообщения об ошибках. `ViewModelChecks` дополнительно проверяет команды, уведомления, применение/сброс, культуру чисел, отмену и ошибки сохранения, независимость вкладок, поиск/страницы, блокировку операций и отмену запросов при закрытии, включая поздние результаты. Реальное шифрование DPAPI проверяется только на Windows, на Linux этот пункт явно пропускается. Опциональный режим `--postgres-fixture` создаёт тестовые схему/таблицу/роль только в пустой одноразовой БД `coordinates_checks`; не запускать его на рабочей БД.
- `CoordinatesTinyApp.WpfChecks` — отдельный консольный проект на `net8.0-windows`, включённый в решение. На Windows в STA-потоке загружает настоящие XAML и общие ресурсы, проверяет привязки обеих вкладок, 16 ячеек, смещений, настроек/PasswordBox и выбора записи, а также отсутствие ошибок WPF Binding. Окна не показываются, рабочие настройки не сохраняются, запросы к БД заменены заглушками. Сборка возможна в Linux-контейнере, запуск — только на Windows. Доступ к внутренним типам приложения предоставлен только этой тестовой сборке через `InternalsVisibleTo`.
- `Invoke-PostgresChecks.ps1` создаёт отдельные контейнер PostgreSQL 17 и Docker-сеть без публикации порта, собирает решение, запускает проверки на синтетических данных и удаляет контейнер и сеть. Проверяет подключение без таблицы, чтение с правами SELECT, страницы/поиск, NULL, неверный XML, ограничения размера, неверный пароль и отмену.
- API, запись в PostgreSQL и другие предметные функции не реализованы.

## Сборка через Docker

Команда для Git Bash из корня репозитория:

```bash
MSYS_NO_PATHCONV=1 docker run --rm -v "$(pwd -W):/src" -w /src mcr.microsoft.com/dotnet/sdk:8.0 dotnet build CoordinatesTinyApp.sln --configuration Release
```

Запуск проверок после сборки (консольный проект, используется `dotnet run`, а не `dotnet test`):

```bash
MSYS_NO_PATHCONV=1 docker run --rm -v "$(pwd -W):/src" -w /src mcr.microsoft.com/dotnet/sdk:8.0 dotnet run --project tests/CoordinatesTinyApp.Checks --configuration Release --no-build
```

`bin` и `obj` являются артефактами сборки и не должны попадать в Git.

Полная проверка с временным PostgreSQL из PowerShell (Docker Desktop должен быть запущен):

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tests/Invoke-PostgresChecks.ps1
```

Для проверки DPAPI без установленного .NET на Windows можно собрать автономные проверки в Docker и запустить их на хосте:

```powershell
docker run --rm -v "${PWD}:/src" -w /src mcr.microsoft.com/dotnet/sdk:8.0 dotnet publish tests/CoordinatesTinyApp.Checks --configuration Release --runtime win-x64 --self-contained true --output /src/artifacts/windows-checks
./artifacts/windows-checks/CoordinatesTinyApp.Checks.exe
```

Результаты в `artifacts/` исключены из Git. Доступа к рабочей БД пока нет: проверки временной таблицы не подтверждают фактическую структуру и права на сервере пользователя.

Проверка XAML-привязок на Windows без установленного .NET (сначала дождаться завершения публикации):

```powershell
docker run --rm -v "${PWD}:/src" -w /src mcr.microsoft.com/dotnet/sdk:8.0 dotnet publish tests/CoordinatesTinyApp.WpfChecks --configuration Release --runtime win-x64 --self-contained true --output /src/artifacts/wpf-checks
./artifacts/wpf-checks/CoordinatesTinyApp.WpfChecks.exe
```

## Правила дальнейшей работы

- Общаться с пользователем на русском языке.
- При изменении структуры, технологических решений или команд сборки обновлять этот файл.
- Не добавлять зависимости без необходимости и подтверждения их совместимости с проектом.
- После изменений выполнять доступные проверки сборки и тестирования, не объявляя непроверенную работу завершённой.
- Не изменять и не откатывать пользовательские правки, не относящиеся к текущей задаче.
