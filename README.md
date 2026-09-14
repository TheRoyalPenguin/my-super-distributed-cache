# my-super-distributed-cache

Делал для себя, чтобы разобраться, как работают большие in-memory кэши.
Вот [презентация](https://docs.google.com/presentation/d/14EHpNJMXKdhNPrR4EXktDv3-CEYAG8bRqdzS94znXnw/edit?usp=drivesdk).
И [design-doc](https://docs.google.com/document/d/1IlkSe9aSCqqbuRmPnbf4OMAbUiZmYNc-xMwYWasbiVc/edit?usp=drivesdk), если кому интересно покопаться в архитектуре.

**Зачем это всё?**
На рынке полно готовых решений. Тот же Redis. Но хотелось собрать свой велосипед с шардированием и динамической оркестрацией контейнеров.

Сразу про ограничения. Код местами сыроват. Тестов пока нет (возможно, когда-нибудь дойдут руки). Работает нормально под Linux. На Windows с пробросом `docker.sock` бывают проблемы со связью, имейте в виду.

**Что умеет:**

* Шардирование. Сделал через кольцо хэшей (MD5).


* У ключей есть TTL. Протухло — удалилось.


* Репликация. Можно задать от 1 до 10 копий на узел.


* Самое веселое: Cluster Manager напрямую общается с докером через `docker.sock`. Он сам поднимает и убивает контейнеры с нодами на лету.


* Авто-ребалансировка. Добавил узел или прибил — данные сами перетекают куда надо.


* Фоновые джобы чекают статус контейнеров. Online, Offline, Error — всё трекается.


* Накидал простенькую админку на React, чтобы визуально оценивать состояние кластера.



**Из чего состоит**

1. **Cluster Manager**. Мозг. REST API, который держит топологию и рулит контейнерами.


2. **Cache Node**. Тупая хранилка. Внутри `ConcurrentDictionary`. Следит за TTL, общается со своими репликами.


3. **Client SDK**. Либа на шарпах (`DistributedCacheClient`), чтобы не писать HTTP-запросы руками.


4. **Admin Panel**. Фронт (React + Vite).



Написано на C# (ASP.NET Core 8.0).

**Как потыкать локально**
Нужны Docker и docker-compose.
Стягиваем репу и запускаем через композ.
Менеджер лезет напрямую в `/var/run/docker.sock`, поэтому на линуксе заведется из коробки.
Админка крутится на `http://localhost:5173`.
Сваггер менеджера — `http://localhost:8080/swagger`.

**Клиент**
Пример работы через SDK:

```csharp
var httpClient = new HttpClient();
var cacheClient = new DistributedCacheClient(httpClient, new Uri("http://localhost:8080"));

await cacheClient.SetAsync("user_123", new { Name = "John" }, ttlSeconds: 60);
var user = await cacheClient.GetAsync<User>("user_123");

```

**Ветки**
В `main` лежит голый скелет. Чисто интерфейсы и локальный in-memory сторадж без сетевого взаимодействия.
Вся магия с докером, репликами и фронтом лежит в `develop`. Смотреть лучше туда.

Пулл-реквесты приветствуются. Если найдете баг — заводите issue.
