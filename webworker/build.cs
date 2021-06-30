// No need for javascript npm/wrangler nonsense.. 

void BuildWorker(string currentDirectory = "d:/work/civilmoney") {
	System.IO.Directory.SetCurrentDirectory(currentDirectory);
	var files = new[] {
	"common/civilmoney-common.js",
	"common/civilmoney-model.js",
	"webworker/ledger.js"
	};
	var s = new System.Text.StringBuilder();
	s.Append("// Generated file - DO NOT MODIFY\r\n");
	foreach(var f in files) {
		s.Append($"\r\n\r\n// ============ {f} ============\r\n\r\n");
		s.Append(System.IO.File.ReadAllText(f));
    }
	System.IO.File.WriteAllText("webworker/index.js", s.ToString());
}
