using Npgsql;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DBConnection
{
    public partial class Form1 : Form
    {
        NpgsqlConnection connection = new NpgsqlConnection();
        string connectionString = ConfigurationManager.ConnectionStrings["YandexCloudConnection"].ConnectionString;
        public Form1()
        {
            connection.StateChange += Connection_StateChange; //для активности и неактивности пункта меню в зависимости от соединения
            InitializeComponent();
        }

        //для активности и неактивности пункта меню в зависимости от соединения
        private void Connection_StateChange(object sender, StateChangeEventArgs e)
        {
            соединитьсяСБазойToolStripMenuItem.Enabled = (e.CurrentState == ConnectionState.Closed);
            отключитьСоединениеToolStripMenuItem.Enabled = (e.CurrentState == ConnectionState.Open);
        }

        private void соединитьсяСБазойToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                if (connection.State != ConnectionState.Open)
                {
                    connection.ConnectionString = connectionString;
                    connection.Open();
                    MessageBox.Show("Соединение с базой данных выполнено успешно");

                    LoadCategories();
                }
                else
                {
                    MessageBox.Show("Соединение с базой данных уже установлено");
                }
            }
            catch (PostgresException ex)
            {
                MessageBox.Show($"PostgreSQL ошибка: {ex.MessageText}\nКод: {ex.SqlState}",
                    "Ошибка PostgreSQL", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            catch (NpgsqlException ex)
            {
                MessageBox.Show($"Ошибка уровня подключения: {ex.Message}",
                                "Ошибка Npgsql", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Неизвестная ошибка: {ex.Message}",
                                "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void отключитьСоединениеToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (connection.State == ConnectionState.Open)
            {
                connection.Close();
                MessageBox.Show("Соединение с базой данных закрыто");
            }
            else
            {
                MessageBox.Show("Соединение уже закрыто");
            }
        }

        private class ComboBoxItem
        {
            public string Text { get; set; }
            public object Value { get; set; }
            public override string ToString() => Text;
        }

        private void LoadCategories()
        {
            cmbCategory.Items.Clear();

            // Добавляем специальный пункт "Все категории"
            cmbCategory.Items.Add(new ComboBoxItem { Text = "Все категории", Value = DBNull.Value });

            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"SELECT categoryid, categoryname FROM ""category""";
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        cmbCategory.Items.Add(new ComboBoxItem
                        {
                            Text = reader["categoryname"].ToString(),
                            Value = reader["categoryid"]
                        });
                    }
                }
            }
            if (cmbCategory.Items.Count > 0)
                cmbCategory.SelectedIndex = 0;
        }
        private void btnLoadProducts_Click(object sender, EventArgs e)
        {
            if (connection.State != ConnectionState.Open)
            {
                MessageBox.Show("Сначала подключитесь к базе данных.");
                return;
            }

            try
            {
                // Очищаем старый список
                listViewProducts.Items.Clear();

                // Создаём команду
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = @"
                SELECT p.productid, p.productname, p.unitprice, c.categoryname
                FROM product p
                LEFT JOIN ""category"" c ON p.categoryid = c.categoryid";

                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var item = new ListViewItem(reader["productname"].ToString()); // 1-я колонка
                            item.SubItems.Add(reader["unitprice"].ToString());             // 2-я колонка
                            item.SubItems.Add(reader["categoryname"].ToString());         // 3-я колонка
                            item.Tag = reader["productid"];

                            listViewProducts.Items.Add(item);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка при получении данных: " + ex.Message);
            }
        }

        private void btnTransaction_Click(object sender, EventArgs e)
        {
            if (connection.State != ConnectionState.Open)
            {
                MessageBox.Show("Сначала подключитесь к базе данных.");
                return;
            }

            // Начинаем транзакцию
            var transaction = connection.BeginTransaction();

            try
            {
                using (var command = connection.CreateCommand())
                {
                    command.Transaction = transaction;

                    // Вставка первого продукта
                    // ⚠️ НЕ УКАЗЫВАЕМ productid — он автоинкремент
                    command.CommandText = @"INSERT INTO ""product"" 
(productname, supplierid, categoryid, quantityperunit, unitprice, unitsinstock, unitsonorder, reorderlevel, discontinued)
VALUES ('Auto Product A', 1, 1, '12 bottles', 12.5, 10, 0, 0, 0);";
                    command.ExecuteNonQuery();

                    command.CommandText = @"INSERT INTO ""product"" 
(productname, supplierid, categoryid, quantityperunit, unitprice, unitsinstock, unitsonorder, reorderlevel, discontinued)
VALUES ('Auto Product B', 1, 1, '6 bottles', 8.5, 7, 0, 0, 0);";
                    command.ExecuteNonQuery();

                    // Коммитим (подтверждаем)
                    transaction.Commit();
                    MessageBox.Show("Оба продукта успешно добавлены в базу.");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка при вставке данных: " + ex.Message);

                try
                {
                    transaction.Rollback();
                    MessageBox.Show("Транзакция отменена.");
                }
                catch (Exception rollbackEx)
                {
                    MessageBox.Show("Ошибка при откате транзакции: " + rollbackEx.Message);
                }
            }

        }

        private void btnDeleteProduct_Click(object sender, EventArgs e)
        {
            if (listViewProducts.SelectedItems.Count == 0)
                return;

            int productId = (int)listViewProducts.SelectedItems[0].Tag;

            var result = MessageBox.Show("Удалить товар?", "Удаление", MessageBoxButtons.YesNo);
            if (result != DialogResult.Yes)
                return;

            using (var command = connection.CreateCommand())
            {
                command.CommandText = "DELETE FROM \"product\" WHERE productid = @id";
                command.Parameters.AddWithValue("id", productId);

                int rows = command.ExecuteNonQuery();
                MessageBox.Show(rows > 0 ? "Удалено!" : "Не найдено.");
                btnLoadProducts.PerformClick();
            }
        }

        private void btnEditProduct_Click(object sender, EventArgs e)
        {
            if (listViewProducts.SelectedItems.Count == 0)
            {
                MessageBox.Show("Выберите товар в списке.");
                return;
            }

            string newName = txtProductName.Text;
            if (!decimal.TryParse(txtPrice.Text, out decimal newPrice))
            {
                MessageBox.Show("Цена некорректна.");
                return;
            }

            int productId = (int)listViewProducts.SelectedItems[0].Tag;
            int categoryId = (int)((ComboBoxItem)cmbCategory.SelectedItem).Value;

            try
            {
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = @"
                UPDATE ""product""
                SET productname = @name, unitprice = @price, categoryid = @cat
                WHERE productid = @id";

                    command.Parameters.AddWithValue("name", newName);
                    command.Parameters.AddWithValue("price", newPrice);
                    command.Parameters.AddWithValue("cat", categoryId);
                    command.Parameters.AddWithValue("id", productId);

                    int rows = command.ExecuteNonQuery();

                    MessageBox.Show(rows > 0 ? "Обновлено!" : "Продукт не найден.");
                    btnLoadProducts.PerformClick();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка обновления: " + ex.Message);
            }
        }

        private void listViewProducts_DoubleClick(object sender, EventArgs e)
        {
            if (listViewProducts.SelectedItems.Count == 0)
                return;

            var selectedItem = listViewProducts.SelectedItems[0];

            // Получаем текст из колонок
            string productName = selectedItem.SubItems[0].Text;
            string unitPrice = selectedItem.SubItems[1].Text;

            // Подставим значения в поля
            txtProductName.Text = productName;
            txtPrice.Text = unitPrice;

            // Получаем productid из Tag
            int productId = (int)selectedItem.Tag;

            // Получаем categoryid по productid
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "SELECT categoryid FROM \"product\" WHERE productid = @id";
                command.Parameters.AddWithValue("id", productId);

                var categoryId = command.ExecuteScalar();

                // Выбираем нужный элемент в ComboBox по значению
                for (int i = 0; i < cmbCategory.Items.Count; i++)
                {
                    if (((ComboBoxItem)cmbCategory.Items[i]).Value.ToString() == categoryId.ToString())
                    {
                        cmbCategory.SelectedIndex = i;
                        break;
                    }
                }
            }
        }

        //загружает данные для фильтрации по категориям
        private void LoadProductsByCategory()
        {
            if (connection.State != ConnectionState.Open)
            {
                MessageBox.Show("Сначала подключитесь к базе данных.");
                return;
            }

            listViewProducts.Items.Clear();

            var selectedCategory = (ComboBoxItem)cmbCategory.SelectedItem;

            using (var command = connection.CreateCommand())
            {
                if (selectedCategory.Value == DBNull.Value)
                {
                    // Без фильтра — все продукты
                    command.CommandText = @"
                SELECT p.productid, p.productname, p.unitprice, c.categoryname
                FROM product p
                LEFT JOIN ""category"" c ON p.categoryid = c.categoryid";
                }
                else
                {
                    command.CommandText = @"
                SELECT p.productid, p.productname, p.unitprice, c.categoryname
                FROM product p
                LEFT JOIN ""category"" c ON p.categoryid = c.categoryid
                WHERE p.categoryid = @catid";
                    command.Parameters.AddWithValue("catid", selectedCategory.Value);
                }

                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var item = new ListViewItem(reader["productname"].ToString());
                        item.SubItems.Add(reader["unitprice"].ToString());
                        item.SubItems.Add(reader["categoryname"].ToString());
                        item.Tag = reader["productid"];
                        listViewProducts.Items.Add(item);
                    }
                }
            }
        }
        private void cmbCategory_SelectedIndexChanged(object sender, EventArgs e)
        {
            LoadProductsByCategory();
        }
    }
}
