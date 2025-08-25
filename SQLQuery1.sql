CREATE TABLE Users ( --Таблица Пользователи
    UserID INT PRIMARY KEY IDENTITY(1,1), -- Уникальный идентификатор пользователя (автоинкрементный)
    Email NVARCHAR(100) UNIQUE NOT NULL, -- Email пользователя (уникальный, не может быть пустым)
    FullName NVARCHAR(255) NOT NULL, -- Полное имя пользователя (не может быть пустым)
    AccountPassword VARCHAR(255) NOT NULL, -- Пароль для входа в аккаунт (не может быть пустым)
);

CREATE TABLE Cards ( -- Таблица Карты
    CardsID INT PRIMARY KEY IDENTITY(1,1), -- Уникальный идентификатор карты
    Number NVARCHAR(16) UNIQUE NOT NULL, -- Номер карты (уникальный, не может быть пустым)
    CardName NVARCHAR(30) NOT NULL, -- Имя карты 
    Cardholder NVARCHAR(100) NOT NULL, -- Держатель карты
    Duration DATE NOT NULL, -- Срок действия
    CVV NVARCHAR(3) NOT NULL, -- CVV-код карты
    Balance DECIMAL(18, 2) NOT NULL -- Баланс карты (в рублях), не может быть пустым
);

CREATE TABLE UserCards ( -- Промежуточная таблица между Users и Cards
    UserID INT NOT NULL,       -- Внешний ключ на пользователя
    CardsID INT NOT NULL,      -- Внешний ключ на карту
    PRIMARY KEY (UserID, CardsID), -- Составной первичный ключ

    FOREIGN KEY (UserID) REFERENCES Users(UserID) ON DELETE CASCADE,
    FOREIGN KEY (CardsID) REFERENCES Cards(CardsID) ON DELETE CASCADE
);

CREATE TABLE Transactions (
    TransactionID INT PRIMARY KEY IDENTITY(1,1),
    UserID INT NOT NULL,
    TransactionType NVARCHAR(50) NOT NULL, -- Тип операции
    Amount DECIMAL(18, 2) NOT NULL,        -- Сумма операции
    TransactionDate DATETIME NOT NULL DEFAULT GETDATE(), -- Дата и время
    SenderCardID INT NULL,                 -- ID карты-источника (если есть)
    Details NVARCHAR(255) NULL,            -- Реквизиты получателя/доп. информация
    Status NVARCHAR(20) NOT NULL DEFAULT 'Completed' 
        CHECK (Status IN ('Completed', 'Failed')), -- Статус с ограничением

    FOREIGN KEY (UserID) REFERENCES Users(UserID),
    FOREIGN KEY (SenderCardID) REFERENCES Cards(CardsID)
);

Select * from UserCards;

select * from Users;
select * from Cards;
Update Users set AccountPassword = '010607aq';
select * from Transactions;

INSERT INTO Cards (Number, CardName, Cardholder, Duration, CVV, Balance) VALUES ('8273673332673645', 'asdlkds', 'JHJHJB JBKKB', '2026-02-01', '234', 35);
insert into UserCards (UserID, CardsID) Values (1,1);

drop table Users;
drop table Cards;
drop table Transactions;
drop table UserCards;

create database banking;

DELETE FROM Users;
delete from Cards;
delete from UserCards;
delete from Transactions;

SELECT * FROM Users;