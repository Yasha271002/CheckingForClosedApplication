using System.IO;
using Newtonsoft.Json;
using System.Text;

namespace CheckingForClosedApplication.Helpers;

public class JsonHelper
{
    public T ReadJsonFromFile<T>(string filePath, T model)
    {
        CheckFileExist(filePath, model);

        var jsonContent = File.ReadAllText(filePath, Encoding.UTF8);
        var deserializedObject = JsonConvert.DeserializeObject<T>(jsonContent);
        return deserializedObject;
    }

    public void WriteJsonToFile<T>(string filePath, T objectToWrite, bool append = false)
    {
        CheckFileExist(filePath, objectToWrite);

        var jsonContent = JsonConvert.SerializeObject(objectToWrite, Formatting.Indented);

        if (append)
            File.AppendAllText(filePath, jsonContent + Environment.NewLine, Encoding.UTF8);
        else
            File.WriteAllText(filePath, jsonContent, Encoding.UTF8);
    }

    private void CheckFileExist<T>(string filePath, T model)
    {
        if (!File.Exists(filePath))
        {
            File.Create(filePath).Dispose();
            WriteJsonToFile(filePath, model, false);
        }
        else
            return;
    }
}