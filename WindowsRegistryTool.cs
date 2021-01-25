using System;
using System.IO;
using System.Linq;
using System.Configuration;
using Microsoft.Win32;
using Microsoft.VisualBasic.FileIO;
using NLog;

namespace WindowsRegistryTool
{
	class RegistryTool
	{
		private readonly static NLog.Logger _log = LogManager.GetCurrentClassLogger();

		private static string registryDataPath = ConfigurationManager.AppSettings["RegistryDataPath"];

		static void Main(string[] args)
		{
			_log.Debug("Main()");
			Init();
		}

		static void Init()
		{
			_log.Debug("Init() - Data path: '{0}'", registryDataPath);

			try
			{
				string[] strArr;
				int counter;

				int baselineRuleTypeCol = 0;
				int ruleSettingCol = 0;
				int expectedResultCol = 0;
				int actualResultCol = 0;

				string registryPath;
				string registryKey;

				string[] filesFromFolder = Directory.GetFiles(registryDataPath);

				foreach (var file in filesFromFolder)
				{
					_log.Debug("Processing file: {0}", file.ToString());

					using (TextFieldParser reader = new TextFieldParser(file))
					{
						reader.TextFieldType = FieldType.Delimited;
						reader.SetDelimiters(",");
						reader.HasFieldsEnclosedInQuotes = true;

						counter = 0;
						while (!reader.EndOfData)
						{
							counter++;
							strArr = reader.ReadFields();

							if (counter == 1)
							{
								if(!strArr.Contains("BaselineRuleType"))
									throw new Exception("Missing column 'BaselineRuleType' in file: " + file.ToString());

								if (!strArr.Contains("RuleSetting"))
									throw new Exception("Missing column 'RuleSetting' in file: " + file.ToString());

								if (!strArr.Contains("ExpectedResult"))
									throw new Exception("Missing column 'ExpectedResult' in file: " + file.ToString());

								if (!strArr.Contains("ActualResult"))
									throw new Exception("Missing column 'ActualResult' in file: " + file.ToString());

								for (int colnum = 0; colnum < strArr.Length; colnum++)
								{
									switch (strArr[colnum])
									{
										case "BaselineRuleType":
											baselineRuleTypeCol = colnum;
											break;

										case "RuleSetting":
											ruleSettingCol = colnum;
											break;

										case "ExpectedResult":
											expectedResultCol = colnum;
											break;

										case "ActualResult":
											actualResultCol = colnum;
											break;
									}
								}

								_log.Debug(
									"Column assignment: BaselineRuleType = {0}, RuleSetting = {1}, ExpectedResult = {2}, ActualResult = {3}",
									baselineRuleTypeCol, ruleSettingCol, expectedResultCol, actualResultCol);
							}
							else
							{
								if (strArr[baselineRuleTypeCol] == "Registry Key")
								{
									string[] rule = strArr[ruleSettingCol].Split(':');

									registryPath = rule[0].Trim();
									registryKey = rule[1].Trim();

									/* _log.Debug(
											"Processing values: strArr[baselineRuleTypeCol] = {0}, registryPath = {1}, registryKey = {2}, strArr[expectedResultCol] = {3}, strArr[actualResultCol] = {4}",
											strArr[baselineRuleTypeCol], registryPath, registryKey, strArr[expectedResultCol], strArr[actualResultCol]); */

									RegistryTool.SaveToRegistry(registryPath, registryKey, strArr[expectedResultCol], strArr[actualResultCol]);
								}
								else
								{
									_log.Debug("Field 'BaselineRuleType' is not type of 'Registry Key' at row: {0}", counter);
								}
							}
						}
					}
				}
			}
			catch (Exception ex)
			{
				_log.Error("Init() - error message: {0}", ex.Message);
			}
		}

		static void SaveToRegistry(string registryPath, string registryKey, string registryValue, string actualResult)
		{
			_log.Debug("SaveToRegistry(registryPath = '{0}', registryKey = '{1}', registryValue = '{2}', actualResult = '{3}'", registryPath, registryKey, registryValue, actualResult);

			try
			{
				string resultValue;
				string previousValue;

				RegistryKey key = RegistryTool.GetRegistryKey(registryPath);

				if (key == null)
					throw new Exception("Registry key not found. registryPath = " + registryPath);

				if(key.GetValue(registryKey) != null)
					previousValue = key.GetValue(registryKey).ToString();
				else
					previousValue = actualResult;

				if (int.TryParse(registryValue, out int registryNumberValue))
				{
					key.SetValue(registryKey, registryNumberValue, RegistryValueKind.DWord);
					_log.Debug("SetValue(registryKey = '{0}', registryNumberValue = '{1}', RegistryValueKind = 'DWord')", registryKey, registryNumberValue);
				}
				else
				{
					key.SetValue(registryKey, registryValue, RegistryValueKind.String);
					_log.Debug("SetValue(registryKey = '{0}', registryNumberValue = '{1}', RegistryValueKind = 'String')", registryKey, registryValue);
				}

				resultValue = key.GetValue(registryKey).ToString();
				_log.Debug("GetValue(registryKey = '{0}') = '{1}'", registryKey, resultValue);

				if(resultValue != null && resultValue == (string) registryValue)
				{
					_log.Info("Value changed successfully for key '{0}' from '{1}' to '{2}'", registryKey, previousValue, resultValue);
				}
				else
				{
					_log.Error("Value not changed for key '{0}'", registryKey);
				}

				key.Close();
			}
			catch (Exception ex)
			{
				_log.Error("SaveToRegistry() - error message: {0}", ex.Message);
			}
		}

		static RegistryKey GetRegistryKey(string registryPath)
		{
			_log.Debug("GetRegistryKey(registryPath = '{0}')", registryPath);

			try
			{
				string registryRoot = registryPath.Split('\\')[0];

				switch (registryRoot)
				{
					case "CurrentUser":
						return Registry.CurrentUser.CreateSubKey(registryPath.Replace(registryRoot + "\\", ""));

					case "LocalMachine":
						return Registry.LocalMachine.CreateSubKey(registryPath.Replace(registryRoot + "\\", ""));

					default:
						return null;
				}
			}
			catch (Exception ex)
			{
				_log.Error("GetRegistryKey() - error message: {0}", ex.Message);
			}

			return null;
		}
	}
}
