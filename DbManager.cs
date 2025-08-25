using System.Data.SqlClient;

namespace WpfApp1
{
    public class DbManager
    {
        private readonly string _connectionString =
            @"Data Source=(localdb)\MSSQLLocalDB;Initial Catalog=banking;Integrated Security=True;Connect Timeout=30;Encrypt=False;";

        // Выполнение скалярного запроса (асинхронно)
        public async Task<object> ExecuteScalarAsync(string query, Dictionary<string, object> parameters)
        {
            using SqlConnection connection = new SqlConnection(_connectionString);
            using SqlCommand command = new SqlCommand(query, connection);
            foreach (var param in parameters)
                command.Parameters.AddWithValue(param.Key, param.Value ?? DBNull.Value);

            await connection.OpenAsync();
            return await command.ExecuteScalarAsync();
        }

        public async Task<string> GetCardNumberByIdAsync(int cardId)
        {
            string query = "SELECT Number FROM Cards WHERE CardsID = @CardID";
            var parameters = new Dictionary<string, object> { { "@CardID", cardId } };

            object result = await ExecuteScalarAsync(query, parameters);
            return result != null && result != DBNull.Value ? result.ToString() : string.Empty;
        }

        public async Task<bool> UserExists(string email)
        {
            string query = "SELECT COUNT(*) FROM Users WHERE Email = @Email";
            var parameters = new Dictionary<string, object> { { "@Email", email } };
            object result = await ExecuteScalarAsync(query, parameters);
            return Convert.ToInt32(result) > 0;
        }

        // Выполнение скалярного запроса (синхронно)
        public object ExecuteScalar(string query, Dictionary<string, object> parameters)
        {
            using SqlConnection connection = new SqlConnection(_connectionString);
            using SqlCommand command = new SqlCommand(query, connection);
            foreach (var param in parameters)
                command.Parameters.AddWithValue(param.Key, param.Value ?? DBNull.Value);

            connection.Open();
            return command.ExecuteScalar();
        }

        // Выполнение запроса с параметрами (асинхронно), возвращает количество затронутых строк
        public async Task<int> ExecuteWithParamsAsync(string query, Dictionary<string, object> parameters)
        {
            using SqlConnection connection = new SqlConnection(_connectionString);
            using SqlCommand command = new SqlCommand(query, connection);
            foreach (var param in parameters)
                command.Parameters.AddWithValue(param.Key, param.Value ?? DBNull.Value);

            await connection.OpenAsync();
            return await command.ExecuteNonQueryAsync();
        }

        // Выполнение запроса с параметрами (синхронно, без возврата)
        public void ActionWithParams(string query, Dictionary<string, object> parameters)
        {
            using SqlConnection connection = new SqlConnection(_connectionString);
            using SqlCommand command = new SqlCommand(query, connection);
            foreach (var param in parameters)
                command.Parameters.AddWithValue(param.Key, param.Value ?? DBNull.Value);

            connection.Open();
            command.ExecuteNonQuery();
        }

        // Получение ID пользователя по email
        public async Task<int> GetUserIdByEmailAsync(string email)
        {
            string query = "SELECT UserID FROM Users WHERE Email = @Email";
            var parameters = new Dictionary<string, object> { { "@Email", email } };

            object result = await ExecuteScalarAsync(query, parameters);
            return result != null ? Convert.ToInt32(result) : -1;
        }

        // Проверка существования пользователя по email
        public async Task<bool> UserExistsAsync(string email)
        {
            string query = "SELECT COUNT(*) FROM Users WHERE Email = @Email";
            var parameters = new Dictionary<string, object> { { "@Email", email } };

            object result = await ExecuteScalarAsync(query, parameters);
            return result != null && Convert.ToInt32(result) > 0;
        }

        // Обновление баланса карты
        public async Task<bool> UpdateCardBalanceAsync(int cardId, decimal amountChange)
        {
            string query = "UPDATE Cards SET Balance = Balance + @Change WHERE CardsID = @CardID";
            var parameters = new Dictionary<string, object>
            {
                { "@Change", amountChange },
                { "@CardID", cardId }
            };

            return await ExecuteWithParamsAsync(query, parameters) > 0;
        }

        // Запись транзакции
        public async Task<bool> RecordTransactionAsync(Transaction transaction)
        {
            string query = @"
    INSERT INTO Transactions 
    (UserID, TransactionType, Amount, SenderCardID, Details, Status)
    VALUES 
    (@UserID, @TransactionType, @Amount, @SenderCardID, @Details, @Status)";

            var parameters = new Dictionary<string, object>
{
    { "@UserID", transaction.UserID },
    { "@TransactionType", transaction.TransactionType },
    { "@Amount", transaction.Amount },
    { "@SenderCardID", transaction.SenderCardID },
    { "@Details", transaction.RecipientDetails ?? string.Empty },
    { "@Status", transaction.Status ?? "Completed" }
};


            return await ExecuteWithParamsAsync(query, parameters) > 0;
        }

        // Получение email пользователя по ID
        public async Task<string?> GetUserEmailAsync(int userId)
        {
            string query = "SELECT Email FROM Users WHERE UserID = @UserID";
            var parameters = new Dictionary<string, object> { { "@UserID", userId } };

            object result = await ExecuteScalarAsync(query, parameters);
            return result?.ToString();
        }


        // Получение всех карт пользователя (для ComboBox)
        public async Task<List<CardInfo>> GetFullUserCardsAsync(int userId)
        {
            string query = @"
                SELECT c.CardsID, c.Number, c.CardName, c.Cardholder, c.Duration, c.CVV, c.Balance
                FROM Cards c
                INNER JOIN UserCards uc ON uc.CardsID = c.CardsID
                WHERE uc.UserID = @UserID";

            List<CardInfo> cards = new List<CardInfo>();

            using SqlConnection connection = new SqlConnection(_connectionString);
            using SqlCommand command = new SqlCommand(query, connection);
            command.Parameters.AddWithValue("@UserID", userId);

            await connection.OpenAsync();
            using SqlDataReader reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                cards.Add(new CardInfo
                {
                    CardsID = reader.GetInt32(0),
                    Number = reader.GetString(1),
                    CardName = reader.GetString(2),
                    Cardholder = reader.GetString(3),
                    Duration = reader.GetDateTime(4),
                    CVV = reader.GetString(5),
                    Balance = reader.GetDecimal(6)
                });
            }

            return cards;
        }

        public async Task<bool> UserHasCardsAsync(int userId)
        {
            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                SqlCommand cmd = new SqlCommand("SELECT COUNT(*) FROM UserCards WHERE UserID = @UserID", conn);
                cmd.Parameters.AddWithValue("@UserID", userId);
                int count = (int)await cmd.ExecuteScalarAsync();
                return count > 0;
            }
        }

        public async Task<bool> ExecuteTransactionAsync(List<string> queries, List<Dictionary<string, object>> parametersList)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        for (int i = 0; i < queries.Count; i++)
                        {
                            using (var command = new SqlCommand(queries[i], connection, transaction))
                            {
                                foreach (var param in parametersList[i])
                                {
                                    command.Parameters.AddWithValue(param.Key, param.Value ?? DBNull.Value);
                                }

                                await command.ExecuteNonQueryAsync();
                            }
                        }

                        transaction.Commit();
                        return true;
                    }
                    catch
                    {
                        transaction.Rollback();
                        return false;
                    }
                }
            }
        }

        public async Task<bool> ExecuteNonQueryAsync(string query, Dictionary<string, object> parameters)
        {
            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    foreach (var param in parameters)
                        cmd.Parameters.AddWithValue(param.Key, param.Value ?? DBNull.Value);

                    int affectedRows = await cmd.ExecuteNonQueryAsync();
                    return affectedRows > 0;
                }
            }
        }

        public async Task<int> GetUserIdByEmail(string email)
        {
            string query = "SELECT UserID FROM Users WHERE Email = @Email";
            var parameters = new Dictionary<string, object> { { "@Email", email } };

            object result = await ExecuteScalarAsync(query, parameters);

            if (result != null && result != DBNull.Value)
                return Convert.ToInt32(result);
            else
                return 0; // или -1, если пользователя нет
        }

        public async Task<List<TransactionViewModel>> GetUserTransactionsAsync(int userId)
        {
            var list = new List<TransactionViewModel>();

            string sql = @"SELECT 
    ISNULL(c.CardName, '-') AS CardName, 
    ISNULL(c.Number, '') AS CardNumber,  
    ISNULL(t.TransactionType, '') AS TransactionType,
    ISNULL(t.Details, '') AS Details,
    t.Amount,
    t.TransactionDate,
    t.Status
FROM Transactions t
LEFT JOIN Cards c ON t.SenderCardID = c.CardsID
WHERE t.UserID = @UserID
ORDER BY t.TransactionDate DESC";

            using (var conn = new SqlConnection(_connectionString))
            using (var cmd = new SqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@UserID", userId);
                await conn.OpenAsync();

                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        list.Add(new TransactionViewModel
                        {
                            CardName = reader.GetString(reader.GetOrdinal("CardName")),
                            TransactionType = reader.GetString(reader.GetOrdinal("TransactionType")),
                            CardNumber = reader.GetString(reader.GetOrdinal("CardNumber")),
                            Details = reader.GetString(reader.GetOrdinal("Details")),
                            Amount = reader.GetDecimal(reader.GetOrdinal("Amount")),
                            TransactionDate = reader.GetDateTime(reader.GetOrdinal("TransactionDate")),
                            Status = reader.GetString(reader.GetOrdinal("Status"))
                        });
                    }
                }
            }

            return list;
        }

        public async Task<Dictionary<string, decimal>> GetSpendingGroupedByTypeAsync(int userId)
        {
            const string sql = @"
                    SELECT TransactionType, SUM(Amount)
                    FROM Transactions
                    WHERE UserID = @UserId AND Status = 'Completed'
                    GROUP BY TransactionType 
                    ";

            var result = new Dictionary<string, decimal>();
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@UserID", userId);
            await conn.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                result[reader.GetString(0)] = reader.GetDecimal(1);
            }
            return result;
        }

        public async Task<decimal?> AutoTopUpIfNeededAsync(int cardId)
        {
            string balanceQuery = "SELECT Balance FROM Cards WHERE CardsID = @CardId";
            var balanceObj = await ExecuteScalarAsync(balanceQuery, new() { { "@CardId", cardId } });

            if (balanceObj != null && decimal.TryParse(balanceObj.ToString(), out decimal balance) && balance < 5)
            {
                Random random = new Random();
                decimal topUpAmount = random.Next(30, 1001); // от 30 до 1000 включительно

                string updateQuery = "UPDATE Cards SET Balance = Balance + @Amount WHERE CardsID = @CardId";
                await ExecuteWithParamsAsync(updateQuery, new()
        {
            { "@Amount", topUpAmount },
            { "@CardId", cardId }
        });

                return topUpAmount; // возвращаем сумму пополнения
            }

            return null; // пополнение не требовалось
        }
    }
}
