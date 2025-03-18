using atmipt.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MySql.Data.MySqlClient;
using System.Diagnostics;
using System.Text;
using System.Security.Cryptography;


namespace atmipt.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;

        public HomeController(ILogger<HomeController> logger)
        {
            _logger = logger;
        }
        public static class BankData
        {
            public static List<BankCard> BankCards { get; set; }
        }

        private string connStr = "server=localhost;database=atm;user=root;password=;";
        public IActionResult Index()
        {
            List<BankCard> bankCards = new List<BankCard>
            {
                new BankCard { Name = "BDO", Color = "#0033A0", Logo = "bdo.png" },
                new BankCard { Name = "China Bank", Color = "#D71A28", Logo = "cbs.png" },
                new BankCard { Name = "PNB", Color = "#004990", Logo = "pnb.png" },
                new BankCard { Name = "LandBank", Color = "#008000", Logo = "landbank.png" },
                new BankCard { Name = "BPI", Color = "#D71A28", Logo = "bpi.png" }
            };
            BankData.BankCards = bankCards;
            return View(bankCards);
        }

        public ActionResult MainMenu(string bank)
        {
            if (string.IsNullOrEmpty(HttpContext.Session.GetString("BankName")))
            {
                return RedirectToAction("Index");
            }
            ViewBag.BankName = HttpContext.Session.GetString("BankName");
            return View();
        }
        public ActionResult CreateAccount()
        {
            return View();
        }
        private string GenerateBankNo()
        {
            Random random = new Random();
            int bankNo = random.Next(1000000, 10000000);
            return bankNo.ToString();
        }

        private string HashPassword(string passcode)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(passcode));
                return BitConverter.ToString(hashedBytes).Replace("-", "").ToLower();
            }
        }

        //Log In-------------------------------------------------------------
        [HttpGet]
        public ActionResult PinEntry(string bank)
        {
            ViewBag.BankName = HttpContext.Session.GetString("BankName");
            ViewBag.BankName = bank;
            if (HomeController.BankData.BankCards != null)
            {
                BankCard bankCard = HomeController.BankData.BankCards.FirstOrDefault(b => b.Name == bank);
                if (bankCard != null)
                {
                    ViewBag.BankLogo = Url.Content("~/images/" + bankCard.Logo); 
                }
            }
            return View();
        }

        [HttpPost]
        public ActionResult PinEntry(string bank, string pin)
        {
            string hashedPin = HashPassword(pin);
            using (MySqlConnection conn = new MySqlConnection(connStr))
            {
                conn.Open();
                string query = "SELECT Id, Name, AccountNo FROM BankAccounts WHERE BankName=@bank AND Pin=@hashedPin";
                using (MySqlCommand cmd = new MySqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@bank", bank);
                    cmd.Parameters.AddWithValue("@hashedPin", hashedPin);
                    using (MySqlDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            HttpContext.Session.SetString("BankName", bank);
                            HttpContext.Session.SetInt32("AccountId", reader.GetInt32("Id"));
                            HttpContext.Session.SetString("UserName", reader.GetString("Name"));
                            HttpContext.Session.SetString("AccountNumber", reader.GetString("AccountNo"));

                            return RedirectToAction("MainMenu", new { bank = bank });
                        }
                    }
                }
            }
            ViewBag.Error = "Invalid PIN. Please try again.";
            ViewBag.BankName = bank;

            if (HomeController.BankData.BankCards != null)
            {
                BankCard bankCard = HomeController.BankData.BankCards.FirstOrDefault(b => b.Name == bank);
                if (bankCard != null)
                {
                    ViewBag.BankLogo = Url.Content("~/images/" + bankCard.Logo);
                }
            }

            return View();
        }

        //Create-------------------------------------------------------------

        [HttpPost]
        public ActionResult CreateAccount(string userName, string passcode, string bankName)
        {
            string hashedPasscode = HashPassword(passcode);
            string bankNo = GenerateBankNo();

            using (MySqlConnection conn = new MySqlConnection(connStr))
            {
                conn.Open();
                try
                {
                    // Check if the PIN already exists
                    string checkQuery = "SELECT COUNT(*) FROM BankAccounts WHERE Pin = @hashedPasscode";
                    using (MySqlCommand checkCmd = new MySqlCommand(checkQuery, conn))
                    {
                        checkCmd.Parameters.AddWithValue("@hashedPasscode", hashedPasscode);
                        int count = Convert.ToInt32(checkCmd.ExecuteScalar());

                        if (count > 0)
                        {
                            ViewBag.Message = "PIN already exists. Please choose a different PIN.";
                            return View();
                        }
                    }

                    string query = "INSERT INTO BankAccounts (Name, Pin, BankName, Balance, AccountNo) VALUES (@userName, @hashedPasscode, @bankName, 0, @bankNo)";
                    using (MySqlCommand cmd = new MySqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@userName", userName);
                        cmd.Parameters.AddWithValue("@hashedPasscode", hashedPasscode);
                        cmd.Parameters.AddWithValue("@bankName", bankName);
                        cmd.Parameters.AddWithValue("@bankNo", bankNo);
                        cmd.ExecuteNonQuery();
                    }

                    ViewBag.Message = "Account created successfully!";
                }
                catch (Exception ex)
                {
                    ViewBag.Message = "An error occurred. Please try again later.";
                }
            }

            return View();
        }
        public ActionResult ChangePin(string bank)

        {

            ViewBag.BankName = HttpContext.Session.GetString("BankName");
            ViewBag.BankName = bank;
            return View();

        }

        // Change PIN------------------------------------------------------
        [HttpPost]
        public ActionResult ChangePin(string currentPin, string newPin, string confirmNewPin)
        {
            int accountId = HttpContext.Session.GetInt32("AccountId") ?? 0;

            if (accountId == 0)
            {
                return RedirectToAction("Index");
            }

            ViewBag.BankName = HttpContext.Session.GetString("BankName"); 
            if (newPin != confirmNewPin)
            {
                ViewBag.Message = "New PIN and Confirm New PIN do not match.";
                return View(); 
            }

            string hashedCurrentPin = HashPassword(currentPin);
            string hashedNewPin = HashPassword(newPin);

            using (MySqlConnection conn = new MySqlConnection(connStr))
            {
                conn.Open();
                try
                {
                    string checkQuery = "SELECT COUNT(*) FROM BankAccounts WHERE Id = @accountId AND Pin = @hashedCurrentPin";
                    using (MySqlCommand checkCmd = new MySqlCommand(checkQuery, conn))
                    {
                        checkCmd.Parameters.AddWithValue("@accountId", accountId);
                        checkCmd.Parameters.AddWithValue("@hashedCurrentPin", hashedCurrentPin);
                        int count = Convert.ToInt32(checkCmd.ExecuteScalar());

                        if (count > 0)
                        {
                            checkQuery = "SELECT COUNT(*) FROM BankAccounts WHERE Pin = @hashedNewPin";
                            using (MySqlCommand checkCmd2 = new MySqlCommand(checkQuery, conn))
                            {
                                checkCmd2.Parameters.AddWithValue("@hashedNewPin", hashedNewPin);
                                count = Convert.ToInt32(checkCmd2.ExecuteScalar());

                                if (count > 0)
                                {
                                    ViewBag.Message = "PIN already exists. Please choose a different PIN.";
                                    return View();
                                }
                            }
                            string updateQuery = "UPDATE BankAccounts SET Pin = @hashedNewPin WHERE Id = @accountId";
                            using (MySqlCommand updateCmd = new MySqlCommand(updateQuery, conn))
                            {
                                updateCmd.Parameters.AddWithValue("@hashedNewPin", hashedNewPin);
                                updateCmd.Parameters.AddWithValue("@accountId", accountId);
                                updateCmd.ExecuteNonQuery();
                            }
                            ViewBag.Message = "PIN changed successfully!";
                        }
                        else
                        {
                            ViewBag.Message = "Incorrect current PIN.";
                        }
                    }
                }
                catch (Exception ex)
                {
                    ViewBag.Message = "An error occurred. Please try again later.";
                }
            }

            return View();
        }

        // Withdraw Cash ----------------------------------------------------------------
        [HttpGet]
        public ActionResult Withdraw(string bank)
        {
            if (string.IsNullOrEmpty(HttpContext.Session.GetString("BankName")))
            {
                return RedirectToAction("Index");
            }
            ViewBag.BankName = HttpContext.Session.GetString("BankName");
            return View();
        }

        [HttpPost]
        public ActionResult Withdraw(string bank, decimal amount, bool printReceipt = false)
        {
            int accountId = HttpContext.Session.GetInt32("AccountId") ?? 0;
            string userName = HttpContext.Session.GetString("UserName");
            string accountNumber = HttpContext.Session.GetString("AccountNumber");

            const decimal transactionFee = 15m; // Define the transaction fee

            if (accountId == 0)
            {
                return RedirectToAction("Index");
            }

            if (amount <= 0)
            {
                ViewBag.Message = "Please enter a valid amount.";
                ViewBag.BankName = HttpContext.Session.GetString("BankName");
                return View();
            }

            if (amount % 100 != 0)
            {
                ViewBag.Message = "Please enter a valid amount.";
                ViewBag.BankName = HttpContext.Session.GetString("BankName");
                return View();
            }

            if (amount > 10000)
            {
                ViewBag.Message = "You can only withdraw up to ?10,000 per transaction.";
                ViewBag.BankName = HttpContext.Session.GetString("BankName");
                return View();
            }

            using (MySqlConnection conn = new MySqlConnection(connStr))
            {
                conn.Open();
                MySqlTransaction transaction = conn.BeginTransaction();

                try
                {
                    decimal currentBalance = 0;
                    string balanceQuery = "SELECT Balance FROM BankAccounts WHERE Id = @accountId";
                    using (MySqlCommand balanceCmd = new MySqlCommand(balanceQuery, conn, transaction))
                    {
                        balanceCmd.Parameters.AddWithValue("@accountId", accountId);
                        using (MySqlDataReader reader = balanceCmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                currentBalance = reader.GetDecimal("Balance");
                            }
                        }
                    }

                    decimal totalDeduction = amount + transactionFee; // Withdrawal amount + fee

                    if (currentBalance >= totalDeduction)
                    {
                        // Deduct both withdrawal amount and transaction fee
                        string updateQuery = "UPDATE BankAccounts SET Balance = Balance - @totalDeduction WHERE Id = @accountId";
                        using (MySqlCommand updateCmd = new MySqlCommand(updateQuery, conn, transaction))
                        {
                            updateCmd.Parameters.AddWithValue("@accountId", accountId);
                            updateCmd.Parameters.AddWithValue("@totalDeduction", totalDeduction);
                            updateCmd.ExecuteNonQuery();
                        }

                        // Log withdrawal transaction
                        string logQuery = "INSERT INTO Transactions (AccountId, Type, Amount, Date) VALUES (@accountId, 'Withdraw', @amount, NOW())";
                        using (MySqlCommand logCmd = new MySqlCommand(logQuery, conn, transaction))
                        {
                            logCmd.Parameters.AddWithValue("@accountId", accountId);
                            logCmd.Parameters.AddWithValue("@amount", amount);
                            logCmd.ExecuteNonQuery();
                        }

                        // Log transaction fee
                        string feeLogQuery = "INSERT INTO Transactions (AccountId, Type, Amount, Date) VALUES (@accountId, 'Transaction Fee', @feeAmount, NOW())";
                        using (MySqlCommand feeLogCmd = new MySqlCommand(feeLogQuery, conn, transaction))
                        {
                            feeLogCmd.Parameters.AddWithValue("@accountId", accountId);
                            feeLogCmd.Parameters.AddWithValue("@feeAmount", transactionFee);
                            feeLogCmd.ExecuteNonQuery();
                        }

                        transaction.Commit();

                        var receiptData = new
                        {
                            Balance = currentBalance - totalDeduction,
                            BankName = bank,
                            UserName = userName,
                            AccountNumber = accountNumber,
                            AccountId = accountId,
                            TransactionType = "Withdrawal",
                            Amount = amount,
                            Fee = transactionFee, // Add transaction fee to the receipt
                            Date = DateTime.Now
                        };

                        TempData["ReceiptData"] = receiptData;
                        ViewBag.ShowReceipt = true;
                        ViewBag.Message = "Withdrawal successful! ?15 transaction fee applied.";
                    }
                    else
                    {
                        ViewBag.Message = "Insufficient balance. Please take your card.";
                    }
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    ViewBag.Message = "An error occurred. Please try again later.";
                }
            }

            ViewBag.BankName = HttpContext.Session.GetString("BankName");
            return View();
        }

        // Check Balance --------------------------------------------------------------------
        public ActionResult CheckBalance(string bank, string accountType = null)
        {
            int accountId = HttpContext.Session.GetInt32("AccountId") ?? 0;
            if (accountId == 0)
            {
                return RedirectToAction("Index");
            }
            decimal balance = 0;
            ViewBag.ShowBalance = false; 

            if (accountType == "Savings")
            {
                using (MySqlConnection conn = new MySqlConnection(connStr))
                {
                    conn.Open();
                    string query = "SELECT Balance FROM BankAccounts WHERE Id = @accountId";
                    using (MySqlCommand cmd = new MySqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@accountId", accountId);
                        object result = cmd.ExecuteScalar();
                        balance = result != null ? Convert.ToDecimal(result) : 0;
                    }
                }
                ViewBag.ShowBalance = true; 
            }
            else if (accountType == "Current")
            {
                balance = 0; 
                ViewBag.ShowBalance = true; 
            }
            ViewBag.BankName = HttpContext.Session.GetString("BankName");
            ViewBag.AccountType = accountType;
            ViewBag.Balance = balance;
            return View();
        }

        // Transfer Funds------------------------------------------------------------------------------
        public ActionResult TransferFunds(string bank)
        {
            if (string.IsNullOrEmpty(HttpContext.Session.GetString("BankName")))
            {
                return RedirectToAction("Index");
            }
            ViewBag.BankName = HttpContext.Session.GetString("BankName");
            return View();

        }

        [HttpPost]
        public ActionResult TransferFunds(string bank, string recipientBankName, string recipientAccountNumber, decimal amount, bool printReceipt = true)
        {
            int accountId = HttpContext.Session.GetInt32("AccountId") ?? 0;
            string userName = HttpContext.Session.GetString("UserName");
            string accountNumber = HttpContext.Session.GetString("AccountNumber");

            if (accountId == 0)
            {
                return RedirectToAction("Index");
            }
            if (amount > 50000)
            {
                ViewBag.Message = "Maximum of 50,000 per transaction";
                ViewBag.BankName = HttpContext.Session.GetString("BankName");
                return View();
            }

            decimal transferFee = 15.00m; 
            decimal totalAmount = amount + transferFee;

            using (MySqlConnection conn = new MySqlConnection(connStr))
            {
                conn.Open();
                MySqlTransaction transaction = conn.BeginTransaction();

                try
                {
                    int recipientAccountId = 0;
                    string recipientName = "";
                    string getRecipientQuery = "SELECT Id, Name FROM BankAccounts WHERE BankName = @recipientBankName AND AccountNo = @recipientAccountNumber";
                    using (MySqlCommand cmd = new MySqlCommand(getRecipientQuery, conn, transaction))
                    {
                        cmd.Parameters.AddWithValue("@recipientBankName", recipientBankName);
                        cmd.Parameters.AddWithValue("@recipientAccountNumber", recipientAccountNumber);
                        using (MySqlDataReader reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                recipientAccountId = reader.GetInt32("Id");
                                recipientName = reader.GetString("Name");
                            }
                            else
                            {
                                ViewBag.Message = "Recipient account not found.";
                                return View();
                            }
                        }
                    }

                    decimal totalDeduction = amount + transferFee;

                    string deductQuery = "UPDATE BankAccounts SET Balance = Balance - @totalDeduction WHERE Id = @accountId AND Balance >= @totalDeduction";
                    using (MySqlCommand cmd = new MySqlCommand(deductQuery, conn, transaction))
                    {
                        cmd.Parameters.AddWithValue("@accountId", accountId);
                        cmd.Parameters.AddWithValue("@totalDeduction", totalDeduction); 
                        int rowsAffected = cmd.ExecuteNonQuery();
                        if (rowsAffected == 0)
                        {
                            ViewBag.Message = "Insufficient balance (including transfer fee).";
                            return View();
                        }
                    }

                    string addQuery = "UPDATE BankAccounts SET Balance = Balance + @amount WHERE Id = @recipientAccountId";
                    using (MySqlCommand cmd = new MySqlCommand(addQuery, conn, transaction))
                    {
                        cmd.Parameters.AddWithValue("@recipientAccountId", recipientAccountId);
                        cmd.Parameters.AddWithValue("@amount", amount); 
                        cmd.ExecuteNonQuery();
                    }

                    string senderLogQuery = "INSERT INTO Transactions (AccountId, Type, Amount, Date) VALUES (@accountId, @type, @amount, NOW())";
                    using (MySqlCommand cmd = new MySqlCommand(senderLogQuery, conn, transaction))
                    {
                        cmd.Parameters.AddWithValue("@accountId", accountId);
                        cmd.Parameters.AddWithValue("@type", $"Transfer to {recipientAccountNumber} ({recipientName}) - Fee: {transferFee}");
                        cmd.Parameters.AddWithValue("@amount", totalDeduction * -1); 
                        cmd.ExecuteNonQuery();
                    }

                    string recipientLogQuery = "INSERT INTO Transactions (AccountId, Type, Amount, Date) VALUES (@recipientAccountId, @type, @amount, NOW())";
                    using (MySqlCommand cmd = new MySqlCommand(recipientLogQuery, conn, transaction))
                    {
                        cmd.Parameters.AddWithValue("@recipientAccountId", recipientAccountId);
                        cmd.Parameters.AddWithValue("@type", $"Transfer from {accountNumber} ({userName})");
                        cmd.Parameters.AddWithValue("@amount", amount);
                        cmd.ExecuteNonQuery();
                    }

                    transaction.Commit();
                    ViewBag.Message = "Transfer successful!";

                    if (printReceipt)
                    {
                        var transferReceiptData = new
                        {
                            BankName = bank,
                            UserName = userName,
                            AccountNumber = accountNumber,
                            TransactionType = $"Transfer to {recipientAccountNumber} ({recipientName})", 
                            Amount = amount,
                            TransferFee = transferFee,
                            TotalAmount = totalAmount,
                            RecipientBankName = recipientBankName,
                            Date = DateTime.Now
                        };

                        TempData["ReceiptData"] = transferReceiptData;
                        ViewBag.ShowReceipt = true;
                    }
                    else
                    {
                        ViewBag.ShowReceiptMessage = false;
                    }
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    ViewBag.Message = "Transfer failed: " + ex.Message;
                }
            }
            ViewBag.BankName = HttpContext.Session.GetString("BankName");
            return View();
        }


        // Mini Statement (Transaction History)-------------------------------------------
        public ActionResult MiniStatement(string bank)
        {
            int accountId = HttpContext.Session.GetInt32("AccountId") ?? 0;
            if (accountId == 0)
            {
                return RedirectToAction("Index");
            }
            List<Transaction> transactions = new List<Transaction>();
            using (MySqlConnection conn = new MySqlConnection(connStr))
            {
                conn.Open();
                string query = "SELECT Date, Type, Amount FROM Transactions WHERE AccountId = @accountId ORDER BY Date DESC LIMIT 5"; 
                using (MySqlCommand cmd = new MySqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@accountId", accountId);
                    using (MySqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            transactions.Add(new Transaction
                            {
                                Date = reader.GetDateTime("Date"),
                                Type = reader.GetString("Type"),
                                Amount = reader.GetDecimal("Amount")
                            });
                        }
                    }
                }
            }
            ViewBag.BankName = HttpContext.Session.GetString("BankName");
            return View(transactions);
        }

        // Cash Deposit -----------------------------------------------------------------------
        public ActionResult Deposit(string bank)
        {
            if (string.IsNullOrEmpty(HttpContext.Session.GetString("BankName")))
            {
                return RedirectToAction("Index");
            }
            ViewBag.BankName = HttpContext.Session.GetString("BankName");
            return View();
        }

        [HttpPost]
        public ActionResult Deposit(string bank, decimal amount)
        {
            int accountId = HttpContext.Session.GetInt32("AccountId") ?? 0;
            string userName = HttpContext.Session.GetString("UserName");
            string accountNumber = HttpContext.Session.GetString("AccountNumber");

            if (accountId == 0)
            {
                return RedirectToAction("Index");
            }

            if (amount <= 0) 
            {
                ViewBag.Message = "Please enter a valid amount.";
                ViewBag.BankName = HttpContext.Session.GetString("BankName");
                return View();
            }

            if (amount % 50 != 0) 
            {
                ViewBag.Message = "Invalid Amount";
                ViewBag.BankName = HttpContext.Session.GetString("BankName");
                return View();
            }
            if (amount < 100)
            {
                ViewBag.Message = "100 pesos minumum deposit";
                ViewBag.BankName = HttpContext.Session.GetString("BankName");
                return View();
            }

            using (MySqlConnection conn = new MySqlConnection(connStr))
            {
                conn.Open();
                MySqlTransaction transaction = conn.BeginTransaction();

                try
                {
                    decimal currentBalance = 0;
                    string balanceQuery = "SELECT Balance FROM BankAccounts WHERE Id = @accountId";
                    using (MySqlCommand balanceCmd = new MySqlCommand(balanceQuery, conn, transaction))
                    {
                        balanceCmd.Parameters.AddWithValue("@accountId", accountId);
                        using (MySqlDataReader reader = balanceCmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                currentBalance = reader.GetDecimal("Balance");
                            }
                        }
                    }

                    string updateQuery = "UPDATE BankAccounts SET Balance = Balance + @amount WHERE Id = @accountId";
                    using (MySqlCommand updateCmd = new MySqlCommand(updateQuery, conn, transaction))
                    {
                        updateCmd.Parameters.AddWithValue("@accountId", accountId);
                        updateCmd.Parameters.AddWithValue("@amount", amount);
                        updateCmd.ExecuteNonQuery();
                    }
                    string logQuery = "INSERT INTO Transactions (AccountId, Type, Amount, Date) VALUES (@accountId, 'Deposit', @amount, NOW())";
                    using (MySqlCommand logCmd = new MySqlCommand(logQuery, conn, transaction))
                    {
                        logCmd.Parameters.AddWithValue("@accountId", accountId);
                        logCmd.Parameters.AddWithValue("@amount", amount);
                        logCmd.ExecuteNonQuery();
                    }

                    transaction.Commit();
                    var receiptData = new
                    {
                        Balance = currentBalance + amount,
                        BankName = bank,
                        UserName = userName,
                        AccountNumber = accountNumber,
                        AccountId = accountId,
                        TransactionType = "Deposit",
                        Amount = amount,
                        Date = DateTime.Now
                    };

                    TempData["ReceiptData"] = receiptData;
                    ViewBag.ShowReceipt = true;
                    ViewBag.Message = "Deposit successful!";
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    ViewBag.Message = "An error occurred. Please try again later.";
                }
            }

            ViewBag.BankName = HttpContext.Session.GetString("BankName");
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

    }
}