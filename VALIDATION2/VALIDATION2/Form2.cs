﻿    using Microsoft.VisualBasic.Devices;
    using MySql.Data.MySqlClient;
    using System.Data;
    using System.IO;
using System.Text;
    using System.Text.RegularExpressions;
using System.Windows.Forms;
using static System.Windows.Forms.DataFormats;

    namespace VALIDATION2
    {
    public partial class Form2 : Form
    {
        private string connectionString = "server=localhost;database=validationfile;uid=root;pwd=12345;";
        private FileSystemWatcher fileWatcher;

        public Form2()
        {
            InitializeComponent();
            InitializeFileWatcher();
            Loadcourse();

            this.FormClosing += Form2_FormClosing;
        }

        private void Form2_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                LogActivity("System closed");
            }
        }
        private void InitializeFileWatcher()
        {
            fileWatcher = new FileSystemWatcher();
            fileWatcher.Path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "RECORDS");
            fileWatcher.Filter = "*.csv";
            fileWatcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size;
            fileWatcher.Changed += OnChanged;
            fileWatcher.Created += OnChanged;
            fileWatcher.Deleted += OnChanged;
            fileWatcher.EnableRaisingEvents = true;
        }

        private void OnChanged(object sender, FileSystemEventArgs e)
        {
            Invoke(new Action(() =>
            {
                LoadTableNames();
                if (comboBox1.SelectedItem != null)
                {
                    string tableName = comboBox1.SelectedItem.ToString();
                    LoadTableData(tableName);
                }
            }));
        }

        private void LoadTableNames()
        {
            using (MySqlConnection connection = new MySqlConnection(connectionString))
            {
                try
                {
                    connection.Open();
                    DataTable tables = connection.GetSchema("Tables");

                    comboBox1.Items.Clear();
                    comboBox1.Items.Add("Select Validation");
                    foreach (DataRow row in tables.Rows)
                    {
                        string tableName = row[2].ToString();
                        if (tableName != "credentials" && tableName != "course" && tableName != "logs")
                        {
                            comboBox1.Items.Add(tableName);
                        }


                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to load table names. Error: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }



     

        private void dataGridView1_CellContentClick(object sender, EventArgs e)
        {

        }
        private void comboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            string tableName = comboBox1.SelectedItem.ToString();
            LoadTableData(tableName);


            if (tableName != "faculty")
            {
                comboBox2.SelectedIndex = 0;
            }
        }


        private void facultyFilters()
        {
            {
                if (loadedTable == null) return;

                DataView view = loadedTable.DefaultView;
                StringBuilder filter = new StringBuilder();

                if (!string.IsNullOrWhiteSpace(textBox1.Text))
                {

                    filter.Append($"(QRCODE LIKE '%{textBox1.Text}%' OR NAME LIKE '%{textBox1.Text}%'  OR UID LIKE '%{textBox1.Text}%')");
                }


                comboBox2.Enabled = false;
                textBox2.Text = "";

                view.RowFilter = filter.ToString();
                dataGridView1.DataSource = view;
            }
        }
        private void ApplyFilters()
        {
            if (loadedTable == null) return;
            comboBox2.Enabled = true;


            DataView view = loadedTable.DefaultView;
            StringBuilder filter = new StringBuilder();

            if (!string.IsNullOrWhiteSpace(textBox1.Text))
            {
                filter.Append($"(QRCODE LIKE '%{textBox1.Text}%' OR TUPCID LIKE '%{textBox1.Text}%' OR UID LIKE '%{textBox1.Text}%' OR STATUS LIKE '%{textBox1.Text}%')");
            }

            if (comboBox2.SelectedItem != null && comboBox2.SelectedItem.ToString() != "Select Course")
            {
                if (filter.Length > 0)
                {
                    filter.Append(" AND ");
                }
                filter.Append($"QRCODE LIKE '%{comboBox2.SelectedItem}%'");
            }

          

            view.RowFilter = filter.ToString();
            dataGridView1.DataSource = view;
            LogActivity("Applied filters to the table");

            UpdateValidationCounts();
        }

        private void UpdateValidationCounts()
        {
            if (loadedTable == null) return;

            int validatedCount = 0;
            int notValidatedCount = 0;
            string selectedCourse = comboBox2.SelectedItem?.ToString();

            foreach (DataRow row in loadedTable.Rows)
            {
                string status = row["STATUS"].ToString();
                string qrcode = row["QRCODE"].ToString();




                if (selectedCourse == "Select Course" || qrcode.Contains(selectedCourse))
                {
                    if (status == "VALIDATED")
                    {
                        validatedCount++;
                    }
                    
                }
            }

            textBox2.Text = $"{validatedCount}";
          
            LogActivity($"Updated validation counts for course: {selectedCourse}");
        }




        private DataTable loadedTable;
        private string tableName;

        private void LoadTableData(string tableName)
        {
            this.tableName = tableName;

            if (tableName == "Select Validation")
            {
                dataGridView1.DataSource = null;
                dataGridView1.Rows.Clear();
                return;
            }

            using (MySqlConnection connection = new MySqlConnection(connectionString))
            {
                try
                {
                    connection.Open();
                    string query = $"SELECT * FROM `{tableName}`";
                    MySqlDataAdapter adapter = new MySqlDataAdapter(query, connection);
                    DataTable table = new DataTable();
                    adapter.Fill(table);


                    DataRow[] filteredRows = table.Select("QRCODE IS NOT NULL AND QRCODE <> ''");
                    DataTable filteredTable = table.Clone();
                    foreach (DataRow row in filteredRows)
                    {
                        filteredTable.ImportRow(row);
                    }

                    loadedTable = filteredTable;
                    LogActivity($"Loaded data for table {tableName}");

                    if (tableName == "faculty")
                    {
                        facultyFilters();
                    }
                    else
                    {
                        dataGridView1.DataSource = loadedTable;
                        ApplyFilters();
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to load data for table {tableName}. Error: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }



        private DataTable FilterDataTable(DataTable dataTable, int columnIndex)
        {
            DataTable filteredTable = dataTable.Clone();
            foreach (DataRow row in dataTable.Rows)
            {
                if (!string.IsNullOrWhiteSpace(row[columnIndex]?.ToString()))
                {
                    filteredTable.ImportRow(row);
                }
            }
            return filteredTable;
        }

        private void Form2_Load(object sender, EventArgs e)
        {
            LoadTableNames();
            Loadcourse();
            comboBox1.SelectedIndex = 0;
            comboBox2.SelectedIndex = 0;
            LogActivity("Validation Program loaded");



        }


        private void button1_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*";
                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    string filePath = openFileDialog.FileName;
                    string fileName = Path.GetFileNameWithoutExtension(filePath);
                    string tableName = GenerateValidTableName(fileName);
                    DataTable csvData = GetDataTableFromCSV(filePath);
                    CreateDatabaseTable(tableName, csvData);
                    InsertDataIntoDatabaseTable(tableName, csvData);
                    MessageBox.Show("DATA SUCCESSFULLY UPLOADED");


                    LoadTableNames();
                    if (comboBox1.Items.Contains(tableName))
                    {
                        comboBox1.SelectedItem = tableName;
                        LoadTableData(tableName);

                    }

                }
            }
        }

        private string GenerateValidTableName(string input)
        {
            return Regex.Replace(input, "[^a-zA-Z0-9_]", "_");
        }

        private void CreateDatabaseTable(string tableName, DataTable table)
        {
            using (MySqlConnection connection = new MySqlConnection(connectionString))
            {
                try
                {
                    connection.Open();
                    string createTableQuery = $"CREATE TABLE IF NOT EXISTS {tableName} (" +
                                              "QRCODE VARCHAR(255), " +
                                              "TUPCID VARCHAR(255), " +
                                              "UID VARCHAR(255), " +
                                              "STATUS VARCHAR(255), " +
                                              "DATETIME DATETIME)";

                    using (MySqlCommand cmd = new MySqlCommand(createTableQuery, connection))
                    {
                        cmd.ExecuteNonQuery();
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to create table. Error: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void InsertDataIntoDatabaseTable(string tableName, DataTable table)
        {
            using (MySqlConnection connection = new MySqlConnection(connectionString))
            {
                try
                {
                    connection.Open();
                    foreach (DataRow row in table.Rows)
                    {
                        string qrcode = row[0].ToString();
                        string tupcid = row[1].ToString();
                        string uid = row[2].ToString();




                        string checkMasterlistQuery = "SELECT COUNT(*) FROM masterlist WHERE QRCODE = @QRCODE AND TUPCID = @TUPCID AND UID = @UID";
                        using (MySqlCommand checkMasterlistCmd = new MySqlCommand(checkMasterlistQuery, connection))
                        {
                            checkMasterlistCmd.Parameters.AddWithValue("@QRCODE", qrcode);
                            checkMasterlistCmd.Parameters.AddWithValue("@TUPCID", tupcid);
                            checkMasterlistCmd.Parameters.AddWithValue("@UID", uid);
                            int masterlistCount = Convert.ToInt32(checkMasterlistCmd.ExecuteScalar());

                            if (masterlistCount == 0)
                            {

                                string insertMasterlistQuery = "INSERT INTO masterlist (QRCODE, TUPCID, UID, STATUS, DATETIME) VALUES (@QRCODE, @TUPCID, @UID, 'ADDED', NOW())";
                                using (MySqlCommand insertMasterlistCmd = new MySqlCommand(insertMasterlistQuery, connection))
                                {
                                    insertMasterlistCmd.Parameters.AddWithValue("@QRCODE", qrcode);
                                    insertMasterlistCmd.Parameters.AddWithValue("@TUPCID", tupcid);
                                    insertMasterlistCmd.Parameters.AddWithValue("@UID", uid);
                                    insertMasterlistCmd.ExecuteNonQuery();
                                    comboBox1.SelectedIndex = 0;
                                    comboBox2.SelectedIndex = 0;
                                }

                            }
                        }


                        string checkQuery = $"SELECT COUNT(*) FROM {tableName} WHERE QRCODE = @QRCODE AND TUPCID = @TUPCID AND UID = @UID";
                        using (MySqlCommand checkCmd = new MySqlCommand(checkQuery, connection))
                        {
                            checkCmd.Parameters.AddWithValue("@QRCODE", qrcode);
                            checkCmd.Parameters.AddWithValue("@TUPCID", tupcid);
                            checkCmd.Parameters.AddWithValue("@UID", uid);
                            int count = Convert.ToInt32(checkCmd.ExecuteScalar());

                            if (count > 0)
                            {

                                string updateQuery = $"UPDATE {tableName} SET STATUS = 'VALIDATED', DATETIME = NOW() WHERE QRCODE = @QRCODE AND TUPCID = @TUPCID AND UID = @UID";
                                using (MySqlCommand updateCmd = new MySqlCommand(updateQuery, connection))
                                {
                                    updateCmd.Parameters.AddWithValue("@QRCODE", qrcode);
                                    updateCmd.Parameters.AddWithValue("@TUPCID", tupcid);
                                    updateCmd.Parameters.AddWithValue("@UID", uid);
                                    updateCmd.ExecuteNonQuery();
                                    comboBox1.SelectedIndex = 0;
                                    comboBox2.SelectedIndex = 0;
                                }

                            }
                            else
                            {

                                string insertQuery = $"INSERT INTO {tableName} (QRCODE, TUPCID, UID, STATUS, DATETIME) VALUES (@QRCODE, @TUPCID, @UID, 'VALIDATED', NOW())";
                                using (MySqlCommand insertCmd = new MySqlCommand(insertQuery, connection))
                                {
                                    insertCmd.Parameters.AddWithValue("@QRCODE", qrcode);
                                    insertCmd.Parameters.AddWithValue("@TUPCID", tupcid);
                                    insertCmd.Parameters.AddWithValue("@UID", uid);
                                    insertCmd.ExecuteNonQuery();
                                    comboBox1.SelectedIndex = 0;
                                    comboBox2.SelectedIndex = 0;
                                }


                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to insert/update data. Error: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private DataTable GetDataTableFromCSV(string filePath)
        {
            DataTable dt = new DataTable();
            int totalRowsInCSV = 0;  // Track total rows read from CSV
            int rowsAddedToDataTable = 0;  // Track rows added to DataTable

            using (StreamReader sr = new StreamReader(filePath))
            {
                if (tableName != "faculty")
                {
                    dt.Columns.Add("QRCODE");
                    dt.Columns.Add("TUPCID");
                    dt.Columns.Add("UID");

                    while (!sr.EndOfStream)
                    {
                        string line = sr.ReadLine();
                        totalRowsInCSV++;  // Increment total row count
                        if (!string.IsNullOrWhiteSpace(line))  // Skip empty rows
                        {
                            string[] rows = line.Split('\t');  // Split using tab delimiter
                            if (rows.Length == dt.Columns.Count)  // Ensure row has the correct number of columns
                            {
                                DataRow dr = dt.NewRow();
                                for (int i = 0; i < rows.Length; i++)
                                {
                                    dr[i] = rows[i];
                                }
                                dt.Rows.Add(dr);
                                rowsAddedToDataTable++;  // Increment DataTable row count
                                Console.WriteLine("Row added: " + string.Join(", ", rows));  // Log added row
                            }
                            else
                            {
                                Console.WriteLine("Skipping row due to column mismatch: " + line);
                            }
                        }
                    }
                }
                else
                {
                    while (!sr.EndOfStream)
                    {
                        string line = sr.ReadLine();
                        totalRowsInCSV++;  // Increment total row count
                        if (!string.IsNullOrWhiteSpace(line))  // Skip empty rows
                        {
                            string[] rows = line.Split('\t');  // Split using tab delimiter
                            if (rows.Length == dt.Columns.Count)  // Ensure row has the correct number of columns
                            {
                                DataRow dr = dt.NewRow();
                                for (int i = 0; i < rows.Length; i++)
                                {
                                    dr[i] = rows[i];
                                }
                                dt.Rows.Add(dr);
                                rowsAddedToDataTable++;  // Increment DataTable row count
                                Console.WriteLine("Row added: " + string.Join(", ", rows));  // Log added row
                            }
                            else
                            {
                                Console.WriteLine("Skipping row due to column mismatch: " + line);
                            }
                        }
                    }
                }
            }

            // Log the row counts for debugging
            Console.WriteLine("Total rows read from CSV: " + totalRowsInCSV);
            Console.WriteLine("Total rows added to DataTable: " + rowsAddedToDataTable);

            return dt;
        }



        private void button2_Click(object sender, EventArgs e)
        {
            LogActivity("User Log out");
            Form1 form1 = new Form1();
            form1.Show();
            this.Hide();
        }

        private void comboBox2_SelectedIndexChanged(object sender, EventArgs e)
        {
            string course = comboBox2.SelectedItem.ToString();
            ApplyFilters();
            LogActivity($"Selected {comboBox2.SelectedItem}");

        }
        private void Loadcourse()
        {
            using (MySqlConnection connection = new MySqlConnection(connectionString))
            {
                try
                {
                    connection.Open();
                    string query = "SELECT * FROM course WHERE STATUS='ENABLED'";
                    MySqlCommand cmd = new MySqlCommand(query, connection);
                    MySqlDataReader reader = cmd.ExecuteReader();

                    comboBox2.Items.Clear();
                    comboBox2.Items.Add("Select Course");
                    while (reader.Read())
                    {
                        string course = reader["COURSE"].ToString();

                        comboBox2.Items.Add(course);
                    }
                }
                catch (Exception ex)
                {

                }
            }
        }

        private void button5_Click(object sender, EventArgs e)
        {
            refresh();
        }
        private void refresh()
        {
            if (comboBox1.SelectedItem != "Select Validation")
            {
                string tableName = comboBox1.SelectedItem.ToString();
                LoadTableData(tableName);
                comboBox2.SelectedIndex = 0;
                textBox1.Text = "";
                LogActivity($"Reload data of the table {comboBox1.SelectedItem}");

            }
            else
            {
                MessageBox.Show("Please select a table from ComboBox1 to reload.", "Information", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void textBox1_TextChanged(object sender, EventArgs e)
        {
            if (tableName != "faculty")
            {

                ApplyFilters();


            }
            else
            {
                facultyFilters();

            }
        }

        private void textBox2_TextChanged(object sender, EventArgs e)
        {

        }

        private void button3_Click(object sender, EventArgs e)
        {

            Form3 form3 = new Form3();
            form3.Form3Closed += Form3_Form3Closed;
            form3.ShowDialog();

        }
        private void LogActivity(string message)
        {
            string logQuery = "INSERT INTO logs (logs, datetime) VALUES (@log, @datetime)";

            using (MySqlConnection connection = new MySqlConnection(connectionString))
            {
                try
                {
                    connection.Open();
                    using (MySqlCommand cmd = new MySqlCommand(logQuery, connection))
                    {
                        cmd.Parameters.AddWithValue("@log", message);
                        cmd.Parameters.AddWithValue("@datetime", DateTime.Now);
                        cmd.ExecuteNonQuery();
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to log activity. Error: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }


        private void Form3_Form3Closed(object sender, EventArgs e)
        {

            LoadTableNames();

            comboBox1.SelectedIndex = 0;
        }

        private void Form4_Form4Closed(object sender, EventArgs e)
        {

            Loadcourse();

        }


        private void Form6_Form6Closed(object sender, EventArgs e)
        {

            Loadcourse();
        }



        private void button6_Click(object sender, EventArgs e)
        {
            Form4 form4 = new Form4();
            form4.Show();
            form4.Form4Closed += Form4_Form4Closed;
        }

        private void button4_Click(object sender, EventArgs e)
        {
            Form5 form5 = new Form5();
            form5.Show();
        }
    }
}

