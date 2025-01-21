
using System.Configuration;
using System.Xml;
using Microsoft.Extensions.Configuration;

var builder = new ConfigurationBuilder()
                 .AddJsonFile($"appsettings.json", true, true);

 var config = builder.Build();
 
string inputFolder = config["FolderPaths:Input"];
string outputFolder = config["FolderPaths:Output"];
string referenceDataFile = config["FolderPaths:ReferenceDataLocation"];

const string ValueFactorHigh = "ValueFactorsHigh";
const string ValueFactorMedium = "ValueFactorsMedium";
const string ValueFactorLow = "ValueFactorsLow";

const string EmissionsFactorHigh = "EmissionsFactorsHigh";
const string EmissionsFactorMedium = "EmissionsFactorMedium";
const string EmissionsFactorLow = "EmissionsFactorsLow";

Dictionary<string, double> referenceData = new Dictionary<string, double>();

if (!Directory.Exists(inputFolder))
{
    Directory.CreateDirectory(inputFolder);
}

if (!Directory.Exists(outputFolder))
{
    Directory.CreateDirectory(outputFolder);
}


FileSystemWatcher watcher = new FileSystemWatcher
{
    Path = inputFolder,
    Filter = "*.xml",
    NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite
};

watcher.Created += OnXmlFileInput;
watcher.EnableRaisingEvents = true;

while (true) { }
;

void OnXmlFileInput(object sender, FileSystemEventArgs e)
{
    ReadReferenceData();
    XmlDocument inputXML = new XmlDocument();
    inputXML.Load(e.FullPath);
    XmlDocument newXMLDocument = CreateNewXMLDocument();

    XmlElement totalsNode = newXMLDocument.CreateElement("Totals");

    XmlNodeList windGenerators = inputXML.SelectNodes("//Wind/WindGenerator");
    if (windGenerators != null)
    {
        foreach (XmlNode windGenerator in windGenerators)
        {
            string generatorName = windGenerator.SelectSingleNode("Name").InnerText;

            XmlNodeList days = windGenerator.SelectNodes("Generation/Day");
            double totalDailyGenerationValue = 0;
            if (days != null)
            {
                foreach (XmlNode day in days)
                {
                    double energy = Convert.ToDouble(day.SelectSingleNode("Energy").InnerText);
                    double price = Convert.ToDouble(day.SelectSingleNode("Price").InnerText);

                    totalDailyGenerationValue += energy * price * GetValueFactor(generatorName.ToLower().Contains("offshore")
                    ? GeneratorType.OffshoreWind
                    : GeneratorType.OnshoreWind);
                }
            }

            XmlElement generatorNode = newXMLDocument.CreateElement("Generator");
            XmlElement name = newXMLDocument.CreateElement("Name");
            name.InnerText = generatorName;
            generatorNode.AppendChild(name);

            XmlElement total = newXMLDocument.CreateElement("Total");
            total.InnerText = totalDailyGenerationValue.ToString();
            generatorNode.AppendChild(total);

            totalsNode.AppendChild(generatorNode);
            //Console.WriteLine("Generator Type:{0}, Total:{1}", generatorName, totalDailyGenerationValue);
        }
    }

    XmlElement maxEmissionGeneratorsNode = newXMLDocument.CreateElement("MaxEmissionGenerators");

    XmlNodeList gasGenerators = inputXML.SelectNodes("//Gas/GasGenerator");
    if (windGenerators != null)
    {
        foreach (XmlNode gasGenerator in gasGenerators)
        {
            string generatorName = gasGenerator.SelectSingleNode("Name").InnerText;
            double emissionsRating = Convert.ToDouble(gasGenerator.SelectSingleNode("EmissionsRating").InnerText);

            XmlNodeList days = gasGenerator.SelectNodes("Generation/Day");
            double totalDailyGenerationValue = 0;
            if (days != null)
            {
                foreach (XmlNode day in days)
                {
                    string date = day.SelectSingleNode("Date").InnerText;
                    double energy = Convert.ToDouble(day.SelectSingleNode("Energy").InnerText);
                    double price = Convert.ToDouble(day.SelectSingleNode("Price").InnerText);

                    totalDailyGenerationValue += energy * price * GetValueFactor(GeneratorType.Gas);
                    if (energy > 0)
                    {
                        XmlElement dayNode = newXMLDocument.CreateElement("Day");

                        XmlElement nameNode = newXMLDocument.CreateElement("Name");
                        nameNode.InnerText = generatorName;
                        dayNode.AppendChild(nameNode);

                        XmlElement dateNode = newXMLDocument.CreateElement("Date");
                        dateNode.InnerText = date;
                        dayNode.AppendChild(dateNode);

                        XmlElement emissionNode = newXMLDocument.CreateElement("Emission");
                        emissionNode.InnerText = (energy * emissionsRating * GetEmissionFactor(GeneratorType.Gas)).ToString();
                        dayNode.AppendChild(emissionNode);

                        maxEmissionGeneratorsNode.AppendChild(dayNode);
                    }
                }
            }

            XmlElement generatorNode = newXMLDocument.CreateElement("Generator");
            XmlElement name = newXMLDocument.CreateElement("Name");
            name.InnerText = generatorName;
            generatorNode.AppendChild(name);

            XmlElement total = newXMLDocument.CreateElement("Total");
            total.InnerText = totalDailyGenerationValue.ToString();
            generatorNode.AppendChild(total);

            totalsNode.AppendChild(generatorNode);

            //Console.WriteLine("Generator Type:{0}, Total:{1}", generatorName, totalDailyGenerationValue);
        }
    }

    XmlNodeList coalGenerators = inputXML.SelectNodes("//Coal/CoalGenerator");
    XmlElement actualHeatRatesNode = newXMLDocument.CreateElement("ActualHeatRates");
    if (windGenerators != null)
    {
        foreach (XmlNode coalGenerator in coalGenerators)
        {
            string generatorName = coalGenerator.SelectSingleNode("Name").InnerText;
            double emissionsRating = Convert.ToDouble(coalGenerator.SelectSingleNode("EmissionsRating").InnerText);
            double totalHeatInput = Convert.ToDouble(coalGenerator.SelectSingleNode("TotalHeatInput").InnerText);
            double actualNetGeneration = Convert.ToDouble(coalGenerator.SelectSingleNode("ActualNetGeneration").InnerText);

            XmlElement actualHeatRateNode = newXMLDocument.CreateElement("ActualHeatRate");
            XmlElement hrNameNode = newXMLDocument.CreateElement("Name");
            hrNameNode.InnerText = generatorName;
            actualHeatRateNode.AppendChild(hrNameNode);

            XmlElement heatRateNode = newXMLDocument.CreateElement("HeatRate");
            heatRateNode.InnerText = (totalHeatInput / actualNetGeneration).ToString();
            actualHeatRateNode.AppendChild(heatRateNode);

            actualHeatRatesNode.AppendChild(actualHeatRateNode);

            XmlNodeList days = coalGenerator.SelectNodes("Generation/Day");
            double totalDailyGenerationValue = 0;
            if (days != null)
            {
                foreach (XmlNode day in days)
                {
                    string date = day.SelectSingleNode("Date").InnerText;
                    double energy = Convert.ToDouble(day.SelectSingleNode("Energy").InnerText);
                    double price = Convert.ToDouble(day.SelectSingleNode("Price").InnerText);

                    totalDailyGenerationValue += energy * price * GetValueFactor(GeneratorType.Coal);
                    if (energy > 0)
                    {
                        XmlElement dayNode = newXMLDocument.CreateElement("Day");

                        XmlElement nameNode = newXMLDocument.CreateElement("Name");
                        nameNode.InnerText = generatorName;
                        dayNode.AppendChild(nameNode);

                        XmlElement dateNode = newXMLDocument.CreateElement("Date");
                        dateNode.InnerText = date;
                        dayNode.AppendChild(dateNode);

                        XmlElement emissionNode = newXMLDocument.CreateElement("Emission");
                        emissionNode.InnerText = (energy * emissionsRating * GetEmissionFactor(GeneratorType.Coal)).ToString();
                        dayNode.AppendChild(emissionNode);

                        maxEmissionGeneratorsNode.AppendChild(dayNode);
                    }
                }
            }

            XmlElement generator = newXMLDocument.CreateElement("Generator");
            XmlElement name = newXMLDocument.CreateElement("Name");
            name.InnerText = generatorName;
            generator.AppendChild(name);

            XmlElement total = newXMLDocument.CreateElement("Total");
            total.InnerText = totalDailyGenerationValue.ToString();
            generator.AppendChild(total);

            totalsNode.AppendChild(generator);

            //Console.WriteLine("Generator Type:{0}, Total:{1}", generatorName, totalDailyGenerationValue);
        }
    }

    newXMLDocument.DocumentElement?.AppendChild(totalsNode);
    newXMLDocument.DocumentElement?.AppendChild(maxEmissionGeneratorsNode);
    newXMLDocument.DocumentElement?.AppendChild(actualHeatRatesNode);

    //Console.WriteLine("Generated XML");
    //Console.WriteLine(newXMLDocument.OuterXml);
    CreateResultXMLFile(e.Name, newXMLDocument);
}


void ReadReferenceData()
{
    if (!File.Exists(referenceDataFile))
    {
        return;
    }
    XmlDocument referenceXML = new XmlDocument();
    referenceXML.Load(referenceDataFile);

    referenceData = new Dictionary<string, double>
    {
        { ValueFactorHigh, Convert.ToDouble(referenceXML.SelectSingleNode("//Factors/ValueFactor/High")?.InnerText) },
        { ValueFactorMedium, Convert.ToDouble(referenceXML.SelectSingleNode("//Factors/ValueFactor/Medium")?.InnerText) },
        { ValueFactorLow, Convert.ToDouble(referenceXML.SelectSingleNode("//Factors/ValueFactor/Low")?.InnerText) },
        { EmissionsFactorHigh, Convert.ToDouble(referenceXML.SelectSingleNode("//Factors/EmissionsFactor/High")?.InnerText) },
        { EmissionsFactorMedium, Convert.ToDouble(referenceXML.SelectSingleNode("//Factors/EmissionsFactor/Medium")?.InnerText) },
        { EmissionsFactorLow, Convert.ToDouble(referenceXML.SelectSingleNode("//Factors/EmissionsFactor/Low")?.InnerText) }
    };


}

double GetValueFactor(GeneratorType generatorType)
{
    double factorValue;
    switch (generatorType)
    {
        case GeneratorType.OffshoreWind:
            factorValue = referenceData[ValueFactorLow];
            break;
        case GeneratorType.OnshoreWind:
            factorValue = referenceData[ValueFactorHigh];
            break;
        case GeneratorType.Gas:
            factorValue = referenceData[ValueFactorMedium];
            break;
        case GeneratorType.Coal:
            factorValue = referenceData[ValueFactorMedium];
            break;
        default: factorValue = 0; break;
    }

    return factorValue;
}

double GetEmissionFactor(GeneratorType generatorType)
{
    double factorValue;
    switch (generatorType)
    {
        case GeneratorType.Gas:
            factorValue = referenceData[EmissionsFactorMedium];
            break;
        case GeneratorType.Coal:
            factorValue = referenceData[EmissionsFactorHigh];
            break;
        default: factorValue = 0; break;
    }

    return factorValue;
}

XmlDocument CreateNewXMLDocument()
{
    XmlDocument newXMLDocument = new XmlDocument();
    XmlDeclaration declaration = newXMLDocument.CreateXmlDeclaration("1.0", "utf-8", null);

    newXMLDocument.AppendChild(declaration);

    XmlElement root = newXMLDocument.CreateElement("GenerationOutput");
    root.SetAttribute("xmlns:xsi", "http://www.w3.org/2001/XMLSchema-instance");
    root.SetAttribute("xmlns:xsd", "http://www.w3.org/2001/XMLSchema");

    newXMLDocument.AppendChild(root);
    return newXMLDocument;
}

void CreateResultXMLFile(string fileName, XmlDocument newXMLDocument)
{
    string newFileName = Path.Combine(outputFolder, fileName.Substring(0, fileName.IndexOf(".")) + "-Result.xml");
    using (XmlWriter writer = XmlWriter.Create(newFileName,
    new XmlWriterSettings
    {
        Indent = true,
        IndentChars = "  ",
        NewLineChars = "\r\n",
        NewLineHandling = NewLineHandling.Replace
    }))
    {
        newXMLDocument.Save(writer);
    }
}