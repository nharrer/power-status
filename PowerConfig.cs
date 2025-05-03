using System.Text;

namespace PowerStatus {

    internal class PowerConfig {

        public Dictionary<string, string> Catagories = new Dictionary<string, string>();
        public bool CanSleep = false;

        public void UpdatePowerRequests() {
            var powerRequests = new List<string>();
            var process = new System.Diagnostics.Process();
            process.StartInfo.FileName = "powercfg";
            process.StartInfo.Arguments = "/requests";
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.CreateNoWindow = true;
            process.Start();
            string output = process.StandardOutput.ReadToEnd();
            string errorOutput = process.StandardError.ReadToEnd();
            if (!string.IsNullOrEmpty(errorOutput)) {
                throw new Exception($"Error executing powercfg: {errorOutput}");
            }
            process.WaitForExit();

            // Parse the output
            var lines = output.Split(new[] { Environment.NewLine }, StringSplitOptions.TrimEntries);

            Catagories.Clear();
            CanSleep = true;

            string? currentCategory = null;
            foreach (var line in lines) {
                if (line.EndsWith(":") && currentCategory == null) {
                    currentCategory = line.TrimEnd(':');
                    Catagories[currentCategory] = string.Empty;
                } else if (string.IsNullOrWhiteSpace(line)) {
                    currentCategory = null;
                } else if (currentCategory != null && !line.Equals("None.")) {
                    CanSleep = false;
                    // add line to Catagories[currentCategory]
                    if (Catagories.ContainsKey(currentCategory)) {
                        Catagories[currentCategory] += line + Environment.NewLine;
                    } else {
                        Catagories[currentCategory] = line + Environment.NewLine;
                    }
                }
            }
        }

        public string GetStatusText() {
            var statusText = new StringBuilder();
            foreach (var category in Catagories) {
                statusText.AppendLine($"{category.Key}:");
                statusText.AppendLine(category.Value);
            }
            return statusText.ToString();
        }
    }
}
