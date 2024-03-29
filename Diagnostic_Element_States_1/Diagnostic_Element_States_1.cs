/*
****************************************************************************
*  Copyright (c) 2021,  Skyline Communications NV  All Rights Reserved.    *
****************************************************************************

By using this script, you expressly agree with the usage terms and
conditions set out below.
This script and all related materials are protected by copyrights and
other intellectual property rights that exclusively belong
to Skyline Communications.

A user license granted for this script is strictly for personal use only.
This script may not be used in any way by anyone without the prior
written consent of Skyline Communications. Any sublicensing of this
script is forbidden.

Any modifications to this script by the user are only allowed for
personal use and within the intended purpose of the script,
and will remain the sole responsibility of the user.
Skyline Communications will not be responsible for any damages or
malfunctions whatsoever of the script resulting from a modification
or adaptation by the user.

The content of this script is confidential information.
The user hereby agrees to keep this confidential information strictly
secret and confidential and not to disclose or reveal it, in whole
or in part, directly or indirectly to any person, entity, organization
or administration without the prior written consent of
Skyline Communications.

Any inquiries can be addressed to:

	Skyline Communications NV
	Ambachtenstraat 33
	B-8870 Izegem
	Belgium
	Tel.	: +32 51 31 35 69
	Fax.	: +32 51 31 01 29
	E-mail	: info@skyline.be
	Web		: www.skyline.be
	Contact	: Ben Vandenberghe

****************************************************************************
Revision History:

DATE		VERSION		AUTHOR			COMMENTS

01/09/2021	1.0.0.1		JLE, Skyline	Initial version
****************************************************************************
*/

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Skyline.DataMiner.Automation;
using Skyline.DataMiner.Library.Automation;
using Skyline.DataMiner.Library.Common;
using Skyline.DataMiner.Net.Messages;
using Skyline.DataMiner.Net;
using SLNetMessages = Skyline.DataMiner.Net.Messages;

/// <summary>
/// DataMiner Script Class.
/// </summary>
class Script
{
	private const string PATH = @"C:\Skyline_Data\Diagnostics\ElementStates\";
	private const string SCRIPT_NAME = "Diagnostics_Element_States";

	/// <summary>
	/// The Script entry point.
	/// </summary>
	/// <param name="engine">Link with SLAutomation process.</param>
	public void Run(Engine engine)
	{
		engine.Timeout = TimeSpan.FromHours(4); //restoring element states might take a very long time on a system with a lot of elements

		try
		{
			RunTest(engine);
		}
		catch (Exception e)
		{
			//fail test because of a general exception    
			engine.ExitFail("An exception occurred:" + e);
		}

		engine.GenerateInformation("Script finished: " + SCRIPT_NAME);
	}

	/// <summary>
	/// Contains the actual test
	/// </summary>
	public void RunTest(Engine engine)
	{
		//Run the actual test

		if (!Directory.Exists(PATH))
		{
			Directory.CreateDirectory(PATH);
		}

		bool bDumpStates = false;

		if (engine.FindInteractiveClient(SCRIPT_NAME, 10, "user:" + engine.UserLoginName, SLNetMessages.AutomationScriptAttachOptions.AttachImmediately))
		{
			engine.GenerateInformation("Attached to user");

			//Present a dialog to the user where he/she can either opt to restore the element states or dump the element states
			#region UIBuilder
			UIBuilder uibActionSelection = new UIBuilder()
			{
				Title = "Element states",
				ColumnDefs = "a",
				RowDefs = "a;a;a;a",
				RequireResponse = true
			};

			UIBlockDefinition blockTextQuestion = new UIBlockDefinition()
			{
				Type = UIBlockType.StaticText,
				Text = "What do you want to do?",
				Width = 350,
				Height = 20,
				Row = 0,
				Column = 0
			};
			uibActionSelection.AppendBlock(blockTextQuestion);

			UIBlockDefinition blockButtonDumpStates = new UIBlockDefinition()
			{
				Type = UIBlockType.Button,
				Text = "Dump element states",
				DestVar = "btnDumpStates",
				Width = 250,
				Height = 25,
				Row = 1,
				Column = 0
			};
			uibActionSelection.AppendBlock(blockButtonDumpStates);

			UIBlockDefinition blockButtonRestoreStates = new UIBlockDefinition()
			{
				Type = UIBlockType.Button,
				Text = "Restore element states from dump file",
				DestVar = "btnRestoreStates",
				Width = 250,
				Height = 25,
				Row = 2,
				Column = 0
			};
			uibActionSelection.AppendBlock(blockButtonRestoreStates);

			UIBlockDefinition blockButtonRestoreStatesFromProperties = new UIBlockDefinition()
			{
				Type = UIBlockType.Button,
				Text = "Restore element states from properties",
				DestVar = "btnRestoreStatesProperties",
				Width = 250,
				Height = 25,
				Row = 3,
				Column = 0
			};
			uibActionSelection.AppendBlock(blockButtonRestoreStatesFromProperties);
			#endregion

			UIResults uirAction = engine.ShowUI(uibActionSelection);
			if (uirAction.WasButtonPressed("btnRestoreStates"))
			{
				//Get the element states backup files for the cluster

				#region UIBuilder
				UIBuilder uibDialogSelectFile = new UIBuilder()
				{
					Title = "Element states restore",
					ColumnDefs = "a;a",
					RowDefs = "a;a;a;a;a",
					RequireResponse = true
				};

				UIBlockDefinition blockText = new UIBlockDefinition()
				{
					Type = UIBlockType.StaticText,
					Text = "Please select a timestamp from which to restore element states:",
					Width = 350,
					Height = 20,
					Row = 1,
					Column = 0,
					ColumnSpan = 2
				};
				uibDialogSelectFile.AppendBlock(blockText);

				UIBlockDefinition blockDropdown = new UIBlockDefinition()
				{
					Type = UIBlockType.DropDown,
					DestVar = "varFile",
					Width = 150,
					Height = 25,
					Row = 2,
					Column = 0,
					ColumnSpan = 2
				};

				bool bHasFiles = false;
				SortedDictionary<string, string> sdBackupFiles = new SortedDictionary<string, string>();
				foreach (string sBackupFile in Directory.GetFiles(PATH, "*-*#*.csv"))
				{
					FileInfo fiBackupFile = new FileInfo(sBackupFile);
					sdBackupFiles.Add(fiBackupFile.Name, fiBackupFile.FullName);
				}
				if (sdBackupFiles.Count > 0)
				{
					bHasFiles = true;
				}
				foreach (string sBackupFile in sdBackupFiles.Keys.Reverse())
				{
					blockDropdown.AddDropDownOption(Path.GetFileNameWithoutExtension(sBackupFile).Replace("#", ":"));
				}

				uibDialogSelectFile.AppendBlock(blockDropdown);

				if (bHasFiles)
				{
					UIBlockDefinition blockButtonRestore = new UIBlockDefinition()
					{
						Type = UIBlockType.Button,
						Text = "Restore",
						DestVar = "btnRestore",
						Width = 100,
						Height = 25,
						Row = 4,
						Column = 0
					};

					uibDialogSelectFile.AppendBlock(blockButtonRestore);
				}
				else
				{
					engine.GenerateInformation("Cluster has no element states backup files. Restore not possible.");
				}

				UIBlockDefinition blockButtonCancel = new UIBlockDefinition()
				{
					Type = UIBlockType.Button,
					Text = "Cancel",
					DestVar = "btnCancel",
					Width = 100,
					Height = 25,
					Row = 4,
					Column = 1
				};
				uibDialogSelectFile.AppendBlock(blockButtonCancel);

				UIBlockDefinition blockTextDisclaimer = new UIBlockDefinition()
				{
					Type = UIBlockType.StaticText,
					Text = "Note that this action might take a very long time on a system with a lot of elements. " +
					"You might want to increase your 'Time before automatic disconnect' setting to 4 hours (which is the timeout time of the script) to avoid the script to stop due to user logout. " +
					"Please do not abort while the script is running. You can follow progress in the RTManager logging.",
					IsMultiline = true,
					Width = 350,
					Height = 95,
					Row = 3,
					Column = 0,
					ColumnSpan = 2
				};
				uibDialogSelectFile.AppendBlock(blockTextDisclaimer);
				#endregion

				//Present a dialog to the user where he/she can do a restore or cancel
				UIResults uirRestore = engine.ShowUI(uibDialogSelectFile);
				if (uirRestore.WasButtonPressed("btnRestore"))
				{
					engine.GenerateInformation("User proceeds with restore from dump file");
					//Do the actual element state restore
					string[] saElementStates;
					saElementStates = File.ReadAllLines(PATH + uirRestore.GetString("varFile").Replace(":", "#") + ".csv");

					DoRestore(engine, saElementStates);
					ShowResultDialog(engine, "Element states restore completed.");
				}
				else if (uirRestore.WasButtonPressed("btnCancel"))
				{
					engine.GenerateInformation("User cancelled");
				}
			}
			else if (uirAction.WasButtonPressed("btnRestoreStatesProperties"))
			{
				engine.GenerateInformation("Starting element state restore from properties");

				#region UIBuilder
				UIBuilder uibDialogSelectTimespan = new UIBuilder()
				{
					Title = "Element states restore",
					ColumnDefs = "a;a",
					RowDefs = "a;a;a;a;a;a",
					RequireResponse = true
				};

				UIBlockDefinition blockText = new UIBlockDefinition()
				{
					Type = UIBlockType.StaticText,
					Text = "Please select the start and end of the timespan in which elements that were stopped, should be started again:",
					Width = 350,
					Height = 40,
					Row = 0,
					Column = 0,
					ColumnSpan = 2
				};
				uibDialogSelectTimespan.AppendBlock(blockText);

				UIBlockDefinition blockText2 = new UIBlockDefinition()
				{
					Type = UIBlockType.StaticText,
					Text = "Start:",
					Width = 100,
					Height = 15,
					Row = 1,
					Column = 0
				};
				uibDialogSelectTimespan.AppendBlock(blockText2);

				UIBlockDefinition blockText3 = new UIBlockDefinition()
				{
					Type = UIBlockType.StaticText,
					Text = "End:",
					Width = 100,
					Height = 15,
					Row = 1,
					Column = 1
				};
				uibDialogSelectTimespan.AppendBlock(blockText3);

				UIBlockDefinition blockStart = new UIBlockDefinition()
				{
					Type = UIBlockType.Calendar,
					DestVar = "tsStart",
					Width = 175,
					Height = 25,
					Row = 2,
					Column = 0
				};
				uibDialogSelectTimespan.AppendBlock(blockStart);

				UIBlockDefinition blockEnd = new UIBlockDefinition()
				{
					Type = UIBlockType.Calendar,
					DestVar = "tsEnd",
					Width = 175,
					Height = 25,
					Row = 2,
					Column = 1
				};
				uibDialogSelectTimespan.AppendBlock(blockEnd);

				UIBlockDefinition blockText4 = new UIBlockDefinition()
				{
					Type = UIBlockType.StaticText,
					Text = "Select the agents in the cluster to restore the states from:",
					Width = 200,
					Height = 40,
					Row = 3,
					Column = 0,
					ColumnSpan = 2
				};
				uibDialogSelectTimespan.AppendBlock(blockText4);

				UIBlockDefinition blockListAgents = new UIBlockDefinition()
				{
					Type = UIBlockType.CheckBoxList,
					DestVar = "lsAgents",
					Width = 150,
					Height = 75,
					Row = 4,
					Column = 0,
					ColumnSpan = 2
				};
				foreach (var dma in Helper.GetDMSAgents(engine))
				{
					blockListAgents.AddCheckBoxListOption(dma.Key.ToString(), dma.Value);
				}
				uibDialogSelectTimespan.AppendBlock(blockListAgents);

				UIBlockDefinition blockButtonRestore = new UIBlockDefinition()
				{
					Type = UIBlockType.Button,
					Text = "Restore",
					DestVar = "btnRestore",
					Width = 100,
					Height = 25,
					Row = 5,
					Column = 0
				};
				uibDialogSelectTimespan.AppendBlock(blockButtonRestore);

				UIBlockDefinition blockButtonCancel = new UIBlockDefinition()
				{
					Type = UIBlockType.Button,
					Text = "Cancel",
					DestVar = "btnCancel",
					Width = 100,
					Height = 25,
					Row = 5,
					Column = 1
				};
				uibDialogSelectTimespan.AppendBlock(blockButtonCancel);
				#endregion

				UIResults uirRestoreFromProperties = engine.ShowUI(uibDialogSelectTimespan);
				if (uirRestoreFromProperties.WasButtonPressed("btnRestore"))
				{
					engine.GenerateInformation("User proceeds with restore from properties");
					//Do the element state restore based on element properties
					//Get selected agent IDs
					string sAgentIds = uirRestoreFromProperties.GetString("lsAgents"); //;-separated
					List<string> lsAgentIds = sAgentIds.Split(';').ToList();

					if (lsAgentIds.Count == 0)
					{
						ShowResultDialog(engine, "No agents were selected");
						engine.GenerateInformation("No agents were selected");
						return;
					}

					List<SLNetMessages.ElementInfoEventMessage> allElements = Helper.GetAllElements(engine).ToList();
					if (allElements == null || allElements.Count == 0)
					{
						ShowResultDialog(engine, "No elements were retrieved from the cluster");
						engine.GenerateInformation("No elements were retrieved from the cluster");
						return;
					}
					else
					{
						engine.GenerateInformation(allElements.Count + " element(s) was/were retrieved");
					}

					Element el;
					//Go through alle elements of the selected agents
					foreach (SLNetMessages.ElementInfoEventMessage element in allElements)
					{
						try
						{
							//we are not interested in elements on DMAs not selected
							if (!lsAgentIds.Contains(element.HostingAgentID.ToString()))
							{
								continue;
							}
							//we are not interested in elements that are not stopped
							if (element.State != SLNetMessages.ElementState.Stopped)
							{
								continue;
							}
							//we are not interesed in elements that were not stopped by DataMiner
							if (!element.GetPropertyValue("State changed by").Equals("dataminer", StringComparison.InvariantCultureIgnoreCase))
							{
								continue;
							}

							string lastChangeTimeString = element.GetPropertyValue("State changed");
							string currentInfo = string.Format("Element \"{0}\" on dma {1} was last changed by DataMiner on \"{2}\". Its current state is \"{3}\"",
								element.Name, element.HostingAgentID, lastChangeTimeString, element.State.ToString());
							// engine.GenerateInformation(currentInfo);

							bool success = DateTime.TryParse(lastChangeTimeString, out DateTime lastChangeTime);
							if (!success)
							{
								throw new Exception(string.Format("Parsing lastchangetime \"{0}\" for element \"{1}\" failed", lastChangeTimeString, element.Name));
							}

							//If the element was stopped by DataMiner during the given timestamp, start the element
							if (lastChangeTime > uirRestoreFromProperties.GetDateTime("tsStart") && lastChangeTime < uirRestoreFromProperties.GetDateTime("tsEnd"))
							{
								// engine.GenerateInformation("Starting element " + element.Name);
								el = engine.FindElement(element.Name);
								el.Start();
								engine.Sleep(2000);
							}
							else
							{
								// engine.GenerateInformation("No need to start element {0}", element.Name);
							}
						}
						catch (Exception ex)
						{
							engine.GenerateInformation(String.Format("Element \"{0}\": {1}", element.Name, ex.Message));
						}
					}

					ShowResultDialog(engine, "Element states restore completed.");
				}
				else if (uirRestoreFromProperties.WasButtonPressed("btnCancel"))
				{
					engine.GenerateInformation("User cancelled");
				}
			}
			else if (uirAction.WasButtonPressed("btnDumpStates"))
			{
				bDumpStates = true;
			}
		}
		else
		{
			//Run in non-interactive mode when triggered by scheduler
			engine.GenerateInformation("Running in non-interactive mode");
			bDumpStates = true;
		}

		if (bDumpStates)
		{
			//Get all element states and dump them to a file in the backup folder for the current cluster

			string sElementStatesFile = DateTime.Now.ToString("yyyy-MM-dd HH#mm#ss") + ".csv";
			engine.GenerateInformation("Starting element state dump procedure");
			List<SLNetMessages.ElementInfoEventMessage> liElements = Helper.GetAllElements(engine).ToList();

			foreach (SLNetMessages.ElementInfoEventMessage element in liElements)
			{
				string sElementName = element.Name;
				SLNetMessages.ElementState elementState = element.State;

				File.AppendAllText(PATH + sElementStatesFile, sElementName + ";" + elementState + Environment.NewLine);
			}

			//Keep last 2 weeks of backups and cleanup older items
			engine.GenerateInformation("Cleaning up old dump files");
			int iKeep = 14;
			SortedDictionary<string, string> sdBackupFiles = new SortedDictionary<string, string>();
			foreach (string sBackupFile in Directory.GetFiles(PATH, "*.csv"))
			{
				FileInfo fiBackupFile = new FileInfo(sBackupFile);
				sdBackupFiles.Add(fiBackupFile.Name, fiBackupFile.FullName);
			}
			while (sdBackupFiles.Count > iKeep)
			{
				File.Delete(sdBackupFiles[sdBackupFiles.Keys.FirstOrDefault()]);
				sdBackupFiles.Remove(sdBackupFiles.Keys.FirstOrDefault());
			}

			if (engine.IsInteractive)
			{
				ShowResultDialog(engine, "Element states dump completed.");
			}
			else
			{
				engine.GenerateInformation("Element states dump completed.");
			}
		}
	}

	public void DoRestore(Engine engine, string[] saElementStates)
	{
		engine.GenerateInformation("Starting element state restore");

		int iElementStartStopSleep = 1000;
		int iElementStopStartSleep = 2000;
		int iElementPauseSleep = 1000;

		int iMaxSimultaneously = 100;
		int iBulkSleep = 5000;

		int iCount = 0;
		bool bStateChanged = false;
		IDms dms = engine.GetDms();
		var elements = dms.GetElements();

		foreach (string s in saElementStates)
		{
			bStateChanged = false;
			try
			{
				string[] sInfo = s.Split(';');
				string sElementName = sInfo[0];
				string sElementState = sInfo[1];
				// engine.GenerateInformation(String.Format("Restore element {0} to state {1}", sElementName, sElementState));

				var element = elements.FirstOrDefault(x => x.Name.Equals(sElementName));
				if (element == null)
				{
					continue;
				}

				switch (sElementState.ToLowerInvariant())
				{
					case "active":
						if (element.State != Skyline.DataMiner.Library.Common.ElementState.Active)
						{
							//engine.GenerateInformation("Start element");
							element.Start();
							bStateChanged = true;
							engine.Sleep(iElementStartStopSleep);
							iCount++;
						}
						else
						{
							// engine.GenerateInformation("Element already started. Not starting again");
						}
						break;
					case "stop":
					case "stopped":
					case "inactive":
						if (element.State != Skyline.DataMiner.Library.Common.ElementState.Stopped)
						{
							// engine.GenerateInformation("Stop element");
							element.Stop();
							bStateChanged = true;
							engine.Sleep(iElementStopStartSleep);
							iCount++;
						}
						else
						{
							// engine.GenerateInformation("Element already stopped. Not stopping again");
						}
						break;
					case "paused":
						if (element.State != Skyline.DataMiner.Library.Common.ElementState.Paused)
						{
							// engine.GenerateInformation("Pause element");
							element.Pause();
							bStateChanged = true;
							engine.Sleep(iElementPauseSleep);
							iCount++;
						}
						else
						{
							// engine.GenerateInformation("Element already paused. Not pausing again.");
						}
						break;
				}
				if (bStateChanged)
				{
					// engine.GenerateInformation(String.Format("Checking new state: element {0} is {1}", sElementName, element.State.ToString()));
				}
			}
			catch (Exception ex)
			{
				engine.GenerateInformation(String.Format("Exception during state restore on line {0}: {1}", s, ex.Message));
			}
			finally
			{
				if (iCount >= iMaxSimultaneously)
				{
					engine.GenerateInformation(String.Format("Maximum simultaneous actions reached. Sleeping for {0} seconds", iBulkSleep / 1000));
					engine.Sleep(iBulkSleep);
					iCount = 0;
				}
			}
		}
	}

	public void ShowResultDialog(Engine engine, string sResultMessage)
	{
		UIBuilder uibResultDialog = new UIBuilder()
		{
			Title = "Element states",
			ColumnDefs = "a",
			RowDefs = "a;a",
			RequireResponse = true
		};


		UIBlockDefinition blockText = new UIBlockDefinition()
		{
			Type = UIBlockType.StaticText,
			Text = sResultMessage,
			Width = 350,
			Height = 20,
			Row = 0,
			Column = 0,
		};
		uibResultDialog.AppendBlock(blockText);


		UIBlockDefinition blockButtonClose = new UIBlockDefinition()
		{
			Type = UIBlockType.Button,
			Text = "Close",
			Width = 100,
			Height = 25,
			Row = 1,
			Column = 0
		};
		uibResultDialog.AppendBlock(blockButtonClose);

		UIResults uirResult = engine.ShowUI(uibResultDialog);
	}
}

public class Helper
{
	/// <summary>
	/// Returns a DMA Object for every agent in the cluster (a failover pair is considered 1 DMA).
	/// </summary>
	public static Dictionary<int, string> GetDMSAgents(Engine engine)
	{
		Dictionary<int, string> agents = new Dictionary<int, string>();

		var dataMinerInfos = engine.SendSLNetMessage(new SLNetMessages.GetInfoMessage(SLNetMessages.InfoType.DataMinerInfo)).Cast<SLNetMessages.GetDataMinerInfoResponseMessage>();

		foreach (var dataMinerInfo in dataMinerInfos)
		{
			// We get two responses for a failover DMA, only add it when we didn't add it yet
			if (agents.Any(a => a.Key == dataMinerInfo.ID))
			{
				continue;
			}

			if (dataMinerInfo.IsFailover)
			{
				agents.Add(dataMinerInfo.ID, dataMinerInfo.AgentName);
			}
			else
			{
				agents.Add(dataMinerInfo.ID, dataMinerInfo.AgentName);
			}
		}

		return agents;
	}

	public static IEnumerable<SLNetMessages.ElementInfoEventMessage> GetAllElements(Engine engine)
	{
		return engine.SendSLNetMessage(new SLNetMessages.GetInfoMessage(SLNetMessages.InfoType.ElementInfo)).Cast<SLNetMessages.ElementInfoEventMessage>();
	}
}
// --- auto-generated code --- do not modify ---

/*
{{StartPackageInfo}}
<PackageInfo xmlns="http://www.skyline.be/ClassLibrary">
	<BasePackage>
		<Identity>
			<Name>Class Library</Name>
			<Version>1.1.2.12</Version>
		</Identity>
	</BasePackage>
	<CustomPackages />
</PackageInfo>
{{EndPackageInfo}}
*/

namespace Skyline.DataMiner.Library
{
	namespace Automation
	{
		/// <summary>
		/// Defines extension methods on the <see cref = "Engine"/> class.
		/// </summary>
		[Skyline.DataMiner.Library.Common.Attributes.DllImport("SLManagedAutomation.dll")]
		[Skyline.DataMiner.Library.Common.Attributes.DllImport("SLNetTypes.dll")]
		public static class EngineExtensions
		{
#pragma warning disable S1104 // Fields should not have public accessibility

#pragma warning disable S2223 // Non-constant static fields should not be visible

			/// <summary>
			/// Allows an override of the behavior of GetDms to return a Fake or Mock of <see cref = "IDms"/>.
			/// Important: When this is used, unit tests should never be run in parallel.
			/// </summary>
			public static System.Func<Skyline.DataMiner.Automation.Engine, Skyline.DataMiner.Library.Common.IDms> OverrideGetDms = engine =>
			{
				return new Skyline.DataMiner.Library.Common.Dms(new Skyline.DataMiner.Library.Common.ConnectionCommunication(Skyline.DataMiner.Automation.Engine.SLNetRaw));
			}

			;
#pragma warning restore S2223 // Non-constant static fields should not be visible

#pragma warning restore S1104 // Fields should not have public accessibility

			/// <summary>
			/// Retrieves an object implementing the <see cref = "IDms"/> interface.
			/// </summary>
			/// <param name = "engine">The <see cref = "Engine"/> instance.</param>
			/// <exception cref = "ArgumentNullException"><paramref name = "engine"/> is <see langword = "null"/>.</exception>
			/// <returns>The <see cref = "IDms"/> object.</returns>
			public static Skyline.DataMiner.Library.Common.IDms GetDms(this Skyline.DataMiner.Automation.Engine engine)
			{
				if (engine == null)
				{
					throw new System.ArgumentNullException("engine");
				}

				return OverrideGetDms(engine);
			}
		}
	}

	namespace Common
	{
		namespace Attributes
		{
			/// <summary>
			/// This attribute indicates a DLL is required.
			/// </summary>
			[System.AttributeUsage(System.AttributeTargets.All, AllowMultiple = true)]
			public sealed class DllImportAttribute : System.Attribute
			{
				/// <summary>
				/// Initializes a new instance of the <see cref = "DllImportAttribute"/> class.
				/// </summary>
				/// <param name = "dllImport">The name of the DLL to be imported.</param>
				public DllImportAttribute(string dllImport)
				{
					DllImport = dllImport;
				}

				/// <summary>
				/// Gets the name of the DLL to be imported.
				/// </summary>
				public string DllImport
				{
					get;
					private set;
				}
			}
		}

		/// <summary>
		/// Represents a system-wide element ID.
		/// </summary>
		/// <remarks>This is a combination of a DataMiner Agent ID (the ID of the Agent on which the element was created) and an element ID.</remarks>
		[System.Serializable]
		[Skyline.DataMiner.Library.Common.Attributes.DllImport("Newtonsoft.Json.dll")]
		public struct DmsElementId : System.IEquatable<Skyline.DataMiner.Library.Common.DmsElementId>, System.IComparable, System.IComparable<Skyline.DataMiner.Library.Common.DmsElementId>
		{
			/// <summary>
			/// The DataMiner Agent ID.
			/// </summary>
			private int agentId;
			/// <summary>
			/// The element ID.
			/// </summary>
			private int elementId;
			/// <summary>
			/// Initializes a new instance of the <see cref = "DmsElementId"/> structure using the specified string.
			/// </summary>
			/// <param name = "id">String representing the system-wide element ID.</param>
			/// <remarks>The provided string must be formatted as follows: "DataMiner Agent ID/element ID (e.g. 400/201)".</remarks>
			/// <exception cref = "ArgumentNullException"><paramref name = "id"/> is <see langword = "null"/> .</exception>
			/// <exception cref = "ArgumentException"><paramref name = "id"/> is the empty string ("") or white space.</exception>
			/// <exception cref = "ArgumentException">The ID does not match the mandatory format.</exception>
			/// <exception cref = "ArgumentException">The DataMiner Agent ID is not an integer.</exception>
			/// <exception cref = "ArgumentException">The element ID is not an integer.</exception>
			/// <exception cref = "ArgumentException">Invalid DataMiner Agent ID.</exception>
			/// <exception cref = "ArgumentException">Invalid element ID.</exception>
			public DmsElementId(string id)
			{
				if (id == null)
				{
					throw new System.ArgumentNullException("id");
				}

				if (System.String.IsNullOrWhiteSpace(id))
				{
					throw new System.ArgumentException("The provided ID must not be empty.", "id");
				}

				string[] idParts = id.Split('/');
				if (idParts.Length != 2)
				{
					string message = System.String.Format(System.Globalization.CultureInfo.InvariantCulture, "Invalid ID. Value: {0}. The string must be formatted as follows: \"agent ID/element ID\".", id);
					throw new System.ArgumentException(message, "id");
				}

				if (!System.Int32.TryParse(idParts[0], System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out agentId))
				{
					string message = System.String.Format(System.Globalization.CultureInfo.InvariantCulture, "Invalid DataMiner agent ID. \"{0}\" is not an integer value", id);
					throw new System.ArgumentException(message, "id");
				}

				if (!System.Int32.TryParse(idParts[1], System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out elementId))
				{
					string message = System.String.Format(System.Globalization.CultureInfo.InvariantCulture, "Invalid Element ID. \"{0}\" is not an integer value", id);
					throw new System.ArgumentException(message, "id");
				}

				if (!IsValidAgentId())
				{
					string message = System.String.Format(System.Globalization.CultureInfo.InvariantCulture, "Invalid agent ID. Value: {0}.", agentId);
					throw new System.ArgumentException(message, "id");
				}

				if (!IsValidElementId())
				{
					string message = System.String.Format(System.Globalization.CultureInfo.InvariantCulture, "Invalid element ID. Value: {0}.", elementId);
					throw new System.ArgumentException(message, "id");
				}
			}

			/// <summary>
			/// Initializes a new instance of the <see cref = "DmsElementId"/> structure using the specified element ID and DataMiner Agent ID.
			/// </summary>
			/// <param name = "agentId">The DataMiner Agent ID.</param>
			/// <param name = "elementId">The element ID.</param>
			/// <remarks>The hosting DataMiner Agent ID value will be set to the same value as the specified DataMiner Agent ID.</remarks>
			/// <exception cref = "ArgumentException"><paramref name = "agentId"/> is invalid.</exception>
			/// <exception cref = "ArgumentException"><paramref name = "elementId"/> is invalid.</exception>
			public DmsElementId(int agentId, int elementId)
			{
				if ((elementId == -1 && agentId != -1) || agentId < -1)
				{
					string message = System.String.Format(System.Globalization.CultureInfo.InvariantCulture, "Invalid agent ID. Value: {0}.", agentId);
					throw new System.ArgumentException(message, "agentId");
				}

				if ((agentId == -1 && elementId != -1) || elementId < -1)
				{
					string message = System.String.Format(System.Globalization.CultureInfo.InvariantCulture, "Invalid element ID. Value: {0}.", elementId);
					throw new System.ArgumentException(message, "elementId");
				}

				this.elementId = elementId;
				this.agentId = agentId;
			}

			/// <summary>
			/// Gets the DataMiner Agent ID.
			/// </summary>
			/// <remarks>The DataMiner Agent ID is the ID of the DataMiner Agent this element has been created on.</remarks>
			public int AgentId
			{
				get
				{
					return agentId;
				}

				private set
				{
					// setter for serialization.
					agentId = value;
				}
			}

			/// <summary>
			/// Gets the element ID.
			/// </summary>
			public int ElementId
			{
				get
				{
					return elementId;
				}

				private set
				{
					// setter for serialization.
					elementId = value;
				}
			}

			/// <summary>
			/// Gets the DataMiner Agent ID/element ID string representation.
			/// </summary>
			[Newtonsoft.Json.JsonIgnore]
			public string Value
			{
				get
				{
					return agentId + "/" + elementId;
				}
			}

			/// <summary>
			/// Compares the current instance with another object of the same type and returns an integer that indicates whether the
			/// current instance precedes, follows, or occurs in the same position in the sort order as the other object.
			/// </summary>
			/// <param name = "other">An object to compare with this instance.</param>
			/// <returns>A value that indicates the relative order of the objects being compared.
			/// The return value has these meanings: Less than zero means this instance precedes <paramref name = "other"/> in the sort order.
			/// Zero means this instance occurs in the same position in the sort order as <paramref name = "other"/>.
			/// Greater than zero means this instance follows <paramref name = "other"/> in the sort order.</returns>
			/// <remarks>The order of the comparison is as follows: DataMiner Agent ID, element ID.</remarks>
			public int CompareTo(Skyline.DataMiner.Library.Common.DmsElementId other)
			{
				int result = agentId.CompareTo(other.AgentId);
				if (result == 0)
				{
					result = elementId.CompareTo(other.ElementId);
				}

				return result;
			}

			/// <summary>
			/// Compares the current instance with another object of the same type and returns an integer that indicates whether the current instance precedes, follows, or occurs in the same position in the sort order as the other object.
			/// </summary>
			/// <param name = "obj">An object to compare with this instance.</param>
			/// <returns>A value that indicates the relative order of the objects being compared. The return value has these meanings: Less than zero means this instance precedes <paramref name = "obj"/> in the sort order. Zero means this instance occurs in the same position in the sort order as <paramref name = "obj"/>. Greater than zero means this instance follows <paramref name = "obj"/> in the sort order.</returns>
			/// <remarks>The order of the comparison is as follows: DataMiner Agent ID, element ID.</remarks>
			/// <exception cref = "ArgumentException">The obj is not of type <see cref = "DmsElementId"/></exception>
			public int CompareTo(object obj)
			{
				if (obj == null)
				{
					return 1;
				}

				if (!(obj is Skyline.DataMiner.Library.Common.DmsElementId))
				{
					throw new System.ArgumentException("The provided object must be of type DmsElementId.", "obj");
				}

				return CompareTo((Skyline.DataMiner.Library.Common.DmsElementId)obj);
			}

			/// <summary>
			/// Compares the object to another object.
			/// </summary>
			/// <param name = "obj">The object to compare against.</param>
			/// <returns><c>true</c> if the elements are equal; otherwise, <c>false</c>.</returns>
			public override bool Equals(object obj)
			{
				if (!(obj is Skyline.DataMiner.Library.Common.DmsElementId))
				{
					return false;
				}

				return Equals((Skyline.DataMiner.Library.Common.DmsElementId)obj);
			}

			/// <summary>
			/// Indicates whether the current object is equal to another object of the same type.
			/// </summary>
			/// <param name = "other">An object to compare with this object.</param>
			/// <returns><c>true</c> if the elements are equal; otherwise, <c>false</c>.</returns>
			public bool Equals(Skyline.DataMiner.Library.Common.DmsElementId other)
			{
				if (elementId == other.elementId && agentId == other.agentId)
				{
					return true;
				}

				return false;
			}

			/// <summary>
			/// Returns the hash code.
			/// </summary>
			/// <returns>The hash code.</returns>
			public override int GetHashCode()
			{
				return elementId ^ agentId;
			}

			/// <summary>
			/// Returns a string that represents the current object.
			/// </summary>
			/// <returns>A string that represents the current object.</returns>
			public override string ToString()
			{
				return System.String.Format(System.Globalization.CultureInfo.InvariantCulture, "agent ID: {0}, element ID: {1}", agentId, elementId);
			}

			/// <summary>
			/// Returns a value determining whether the agent ID is valid.
			/// </summary>
			/// <returns><c>true</c> if the agent ID is valid; otherwise, <c>false</c>.</returns>
			private bool IsValidAgentId()
			{
				bool isValid = true;
				if ((elementId == -1 && agentId != -1) || agentId < -1)
				{
					isValid = false;
				}

				return isValid;
			}

			/// <summary>
			/// Returns a value determining whether the element ID is valid.
			/// </summary>
			/// <returns><c>true</c> if the element ID is valid; otherwise, <c>false</c>.</returns>
			private bool IsValidElementId()
			{
				bool isValid = true;
				if ((agentId == -1 && elementId != -1) || elementId < -1)
				{
					isValid = false;
				}

				return isValid;
			}
		}

		/// <summary>
		/// Represents a DataMiner System.
		/// </summary>
		internal class Dms : Skyline.DataMiner.Library.Common.IDms
		{
			/// <summary>
			/// Cached element information message.
			/// </summary>
			private Skyline.DataMiner.Net.Messages.ElementInfoEventMessage cachedElementInfoMessage;
			/// <summary>
			/// Cached DataMiner information message.
			/// </summary>
			private Skyline.DataMiner.Net.Messages.GetDataMinerInfoResponseMessage cachedDataMinerAgentMessage;
			/// <summary>
			/// Cached protocol information message.
			/// </summary>
			private Skyline.DataMiner.Net.Messages.GetProtocolInfoResponseMessage cachedProtocolMessage;
			/// <summary>
			/// Cached protocol requested version.
			/// </summary>
			private string cachedProtocolRequestedVersion;
			/// <summary>
			/// The object used for DMS communication.
			/// </summary>
			private Skyline.DataMiner.Library.Common.ICommunication communication;
			/// <summary>
			/// Initializes a new instance of the <see cref = "Dms"/> class.
			/// </summary>
			/// <param name = "communication">An object implementing the ICommunication interface.</param>
			/// <exception cref = "ArgumentNullException"><paramref name = "communication"/> is <see langword = "null"/>.</exception>
			internal Dms(Skyline.DataMiner.Library.Common.ICommunication communication)
			{
				if (communication == null)
				{
					throw new System.ArgumentNullException("communication");
				}

				this.communication = communication;
			}

			/// <summary>
			/// Gets the communication interface.
			/// </summary>
			/// <value>The communication interface.</value>
			public Skyline.DataMiner.Library.Common.ICommunication Communication
			{
				get
				{
					return communication;
				}
			}

			/// <summary>
			/// Determines whether a DataMiner Agent with the specified ID is present in the DataMiner System.
			/// </summary>
			/// <param name = "agentId">The DataMiner Agent ID.</param>
			/// <exception cref = "ArgumentException"><paramref name = "agentId"/> is invalid.</exception>
			/// <returns><c>true</c> if the DataMiner Agent ID is valid; otherwise, <c>false</c>.</returns>
			public bool AgentExists(int agentId)
			{
				if (agentId < 1)
				{
					throw new System.ArgumentException(System.String.Format(System.Globalization.CultureInfo.InvariantCulture, "DataMiner agent ID: {0} is invalid", agentId), "agentId");
				}

				try
				{
					Skyline.DataMiner.Net.Messages.GetDataMinerByIDMessage message = new Skyline.DataMiner.Net.Messages.GetDataMinerByIDMessage(agentId);
					cachedDataMinerAgentMessage = communication.SendSingleResponseMessage(message) as Skyline.DataMiner.Net.Messages.GetDataMinerInfoResponseMessage;
					return cachedDataMinerAgentMessage != null;
				}
				catch (Skyline.DataMiner.Net.Exceptions.DataMinerException e)
				{
					if (e.ErrorCode == -2146233088)
					{
						// 0x80131500, No agent available with ID.
						return false;
					}
					else
					{
						throw;
					}
				}
			}

			/// <summary>
			/// Determines whether an element with the specified Agent ID/element ID exists in the DataMiner System.
			/// </summary>
			/// <param name = "dmsElementId">The DataMiner Agent ID/element ID of the element.</param>
			/// <returns><c>true</c> if the element exists; otherwise, <c>false</c>.</returns>
			/// <exception cref = "ArgumentException"><paramref name = "dmsElementId"/> is invalid.</exception>
			public bool ElementExists(Skyline.DataMiner.Library.Common.DmsElementId dmsElementId)
			{
				int dmaId = dmsElementId.AgentId;
				int elementId = dmsElementId.ElementId;
				if (dmaId < 1)
				{
					throw new System.ArgumentException(System.String.Format(System.Globalization.CultureInfo.InvariantCulture, "Invalid DataMiner agent ID: {0}", dmaId), "dmsElementId");
				}

				if (elementId < 1)
				{
					throw new System.ArgumentException(System.String.Format(System.Globalization.CultureInfo.InvariantCulture, "Invalid DataMiner element ID: {0}", elementId), "dmsElementId");
				}

				try
				{
					Skyline.DataMiner.Net.Messages.GetElementByIDMessage message = new Skyline.DataMiner.Net.Messages.GetElementByIDMessage(dmaId, elementId);
					Skyline.DataMiner.Net.Messages.ElementInfoEventMessage response = (Skyline.DataMiner.Net.Messages.ElementInfoEventMessage)Communication.SendSingleResponseMessage(message);
					// Cache the response of SLNet.
					// Could be useful when this call is used within a "GetElement" this makes sure that we do not double call SLNet.
					if (response != null)
					{
						cachedElementInfoMessage = response;
						return true;
					}
					else
					{
						return false;
					}
				}
				catch (Skyline.DataMiner.Net.Exceptions.DataMinerException e)
				{
					if (e.ErrorCode == -2146233088)
					{
						// 0x80131500, Element "[element name]" is unavailable.
						return false;
					}
					else
					{
						throw;
					}
				}
			}

			/// <summary>
			/// Retrieves all elements from the DataMiner System.
			/// </summary>
			/// <returns>The elements present on the DataMiner System.</returns>
			public System.Collections.Generic.ICollection<Skyline.DataMiner.Library.Common.IDmsElement> GetElements()
			{
				Skyline.DataMiner.Net.Messages.GetInfoMessage message = new Skyline.DataMiner.Net.Messages.GetInfoMessage { Type = Skyline.DataMiner.Net.Messages.InfoType.ElementInfo };
				Skyline.DataMiner.Net.Messages.DMSMessage[] responses = communication.SendMessage(message);
				System.Collections.Generic.List<Skyline.DataMiner.Library.Common.IDmsElement> elements = new System.Collections.Generic.List<Skyline.DataMiner.Library.Common.IDmsElement>();
				foreach (Skyline.DataMiner.Net.Messages.DMSMessage response in responses)
				{
					Skyline.DataMiner.Net.Messages.ElementInfoEventMessage elementInfo = (Skyline.DataMiner.Net.Messages.ElementInfoEventMessage)response;
					if (elementInfo.DataMinerID == -1 || elementInfo.ElementID == -1)
					{
						continue;
					}

					try
					{
						Skyline.DataMiner.Library.Common.DmsElement element = new Skyline.DataMiner.Library.Common.DmsElement(this, elementInfo);
						elements.Add(element);
					}
					catch (System.Exception ex)
					{
						string logMessage = "Failed parsing element info for element " + System.Convert.ToString(elementInfo.Name) + " (" + System.Convert.ToString(elementInfo.DataMinerID) + "/" + System.Convert.ToString(elementInfo.ElementID) + ")." + System.Environment.NewLine + ex;
						Skyline.DataMiner.Library.Common.Logger.Log(logMessage);
					}
				}

				return elements;
			}

			/// <summary>
			/// Determines whether the specified version of the specified protocol exists.
			/// </summary>
			/// <param name = "protocolName">The protocol name.</param>
			/// <param name = "protocolVersion">The protocol version.</param>
			/// <exception cref = "ArgumentNullException"><paramref name = "protocolName"/> is <see langword = "null"/>.</exception>
			/// <exception cref = "ArgumentNullException"><paramref name = "protocolVersion"/> is <see langword = "null"/>.</exception>
			/// <exception cref = "ArgumentException"><paramref name = "protocolName"/> is the empty string ("") or white space.</exception>
			/// <exception cref = "ArgumentException"><paramref name = "protocolVersion"/> is the empty string ("") or white space.</exception>
			/// <returns><c>true</c> if the protocol is valid; otherwise, <c>false</c>.</returns>
			public bool ProtocolExists(string protocolName, string protocolVersion)
			{
				if (protocolName == null)
				{
					throw new System.ArgumentNullException("protocolName");
				}

				if (protocolVersion == null)
				{
					throw new System.ArgumentNullException("protocolVersion");
				}

				if (System.String.IsNullOrWhiteSpace(protocolName))
				{
					throw new System.ArgumentException("The name of the protocol is the empty string (\"\") or white space.", "protocolName");
				}

				if (System.String.IsNullOrWhiteSpace(protocolVersion))
				{
					throw new System.ArgumentException("The version of the protocol is the empty string (\"\") or white space.", "protocolVersion");
				}

				cachedProtocolRequestedVersion = protocolVersion;
				Skyline.DataMiner.Net.Messages.GetProtocolMessage message = new Skyline.DataMiner.Net.Messages.GetProtocolMessage { Protocol = protocolName, Version = cachedProtocolRequestedVersion };
				cachedProtocolMessage = (Skyline.DataMiner.Net.Messages.GetProtocolInfoResponseMessage)communication.SendSingleResponseMessage(message);
				return cachedProtocolMessage != null;
			}

			/// <summary>
			/// Returns a string that represents the current object.
			/// </summary>
			/// <returns>A string that represents the current object.</returns>
			public override string ToString()
			{
				return "DataMiner System";
			}
		}

		/// <summary>
		/// Helper class to convert from enumeration value to string or vice versa.
		/// </summary>
		internal static class EnumMapper
		{
			/// <summary>
			/// The connection type map.
			/// </summary>
			private static readonly System.Collections.Generic.Dictionary<System.String, Skyline.DataMiner.Library.Common.ConnectionType> ConnectionTypeMapping = new System.Collections.Generic.Dictionary<System.String, Skyline.DataMiner.Library.Common.ConnectionType> { { "SNMP", Skyline.DataMiner.Library.Common.ConnectionType.SnmpV1 }, { "SNMPV1", Skyline.DataMiner.Library.Common.ConnectionType.SnmpV1 }, { "SNMPV2", Skyline.DataMiner.Library.Common.ConnectionType.SnmpV2 }, { "SNMPV3", Skyline.DataMiner.Library.Common.ConnectionType.SnmpV3 }, { "SERIAL", Skyline.DataMiner.Library.Common.ConnectionType.Serial }, { "SERIAL SINGLE", Skyline.DataMiner.Library.Common.ConnectionType.SerialSingle }, { "SMART-SERIAL", Skyline.DataMiner.Library.Common.ConnectionType.SmartSerial }, { "SMART-SERIAL SINGLE", Skyline.DataMiner.Library.Common.ConnectionType.SmartSerialSingle }, { "HTTP", Skyline.DataMiner.Library.Common.ConnectionType.Http }, { "GPIB", Skyline.DataMiner.Library.Common.ConnectionType.Gpib }, { "VIRTUAL", Skyline.DataMiner.Library.Common.ConnectionType.Virtual }, { "OPC", Skyline.DataMiner.Library.Common.ConnectionType.Opc }, { "SLA", Skyline.DataMiner.Library.Common.ConnectionType.Sla }, { "WEBSOCKET", Skyline.DataMiner.Library.Common.ConnectionType.WebSocket } };
			/// <summary>
			/// Converts a string denoting a connection type to the corresponding value of the <see cref = "ConnectionType"/> enumeration.
			/// </summary>
			/// <param name = "type">The connection type.</param>
			/// <exception cref = "ArgumentNullException"><paramref name = "type"/> is <see langword = "null"/>.</exception>
			/// <exception cref = "ArgumentException"><paramref name = "type"/> is the empty string ("") or white space</exception>
			/// <exception cref = "KeyNotFoundException"></exception>
			/// <returns>The corresponding <see cref = "ConnectionType"/> value.</returns>
			internal static Skyline.DataMiner.Library.Common.ConnectionType ConvertStringToConnectionType(string type)
			{
				if (type == null)
				{
					throw new System.ArgumentNullException("type");
				}

				if (System.String.IsNullOrWhiteSpace(type))
				{
					throw new System.ArgumentException("The type must not be empty.", "type");
				}

				string valueLower = type.ToUpperInvariant();
				Skyline.DataMiner.Library.Common.ConnectionType result;
				if (!ConnectionTypeMapping.TryGetValue(valueLower, out result))
				{
					throw new System.Collections.Generic.KeyNotFoundException(System.String.Format(System.Globalization.CultureInfo.InvariantCulture, "The key {0} could not be found.", valueLower));
				}

				return result;
			}
		}

		/// <summary>
		/// Class containing helper methods.
		/// </summary>
		internal static class HelperClass
		{
			/// <summary>
			/// Determines if a connection is using a dedicated connection or not (e.g serial single, smart serial single).
			/// </summary>
			/// <param name = "info">ElementPortInfo</param>
			/// <returns>Whether a connection is marked as single or not.</returns>
			internal static bool IsDedicatedConnection(Skyline.DataMiner.Net.Messages.ElementPortInfo info)
			{
				bool isDedicatedConnection = false;
				switch (info.ProtocolType)
				{
					case Skyline.DataMiner.Net.Messages.ProtocolType.SerialSingle:
					case Skyline.DataMiner.Net.Messages.ProtocolType.SmartSerialRawSingle:
					case Skyline.DataMiner.Net.Messages.ProtocolType.SmartSerialSingle:
						isDedicatedConnection = true;
						break;
					default:
						isDedicatedConnection = false;
						break;
				}

				return isDedicatedConnection;
			}
		}

		/// <summary>
		///     DataMiner System interface.
		/// </summary>
		public interface IDms
		{
			/// <summary>
			///     Gets the communication interface.
			/// </summary>
			/// <value>The communication interface.</value>
			Skyline.DataMiner.Library.Common.ICommunication Communication
			{
				get;
			}

			/// <summary>
			///     Determines whether a DataMiner Agent with the specified ID is present in the DataMiner System.
			/// </summary>
			/// <param name = "agentId">The DataMiner Agent ID.</param>
			/// <exception cref = "ArgumentException">The DataMiner Agent ID is negative.</exception>
			/// <returns><c>true</c> if the DataMiner Agent ID is valid; otherwise, <c>false</c>.</returns>
			bool AgentExists(int agentId);
			/// <summary>
			///     Determines whether an element with the specified DataMiner Agent ID/element ID exists in the DataMiner System.
			/// </summary>
			/// <param name = "dmsElementId">The DataMiner Agent ID/element ID of the element.</param>
			/// <returns><c>true</c> if the element exists; otherwise, <c>false</c>.</returns>
			bool ElementExists(Skyline.DataMiner.Library.Common.DmsElementId dmsElementId);
			/// <summary>
			///     Retrieves all elements from the DataMiner System.
			/// </summary>
			/// <returns>The elements present on the DataMiner System.</returns>
			System.Collections.Generic.ICollection<Skyline.DataMiner.Library.Common.IDmsElement> GetElements();
			/// <summary>
			///     Determines whether the specified version of the specified protocol exists.
			/// </summary>
			/// <param name = "protocolName">The protocol name.</param>
			/// <param name = "protocolVersion">The protocol version.</param>
			/// <exception cref = "ArgumentNullException"><paramref name = "protocolName"/> is <see langword = "null"/>.</exception>
			/// <exception cref = "ArgumentNullException"><paramref name = "protocolVersion"/> is <see langword = "null"/>.</exception>
			/// <exception cref = "ArgumentException"><paramref name = "protocolName"/> is the empty string ("") or white space.</exception>
			/// <exception cref = "ArgumentException"><paramref name = "protocolVersion"/> is the empty string ("") or white space.</exception>
			/// <returns><c>true</c> if the protocol is valid; otherwise, <c>false</c>.</returns>
			bool ProtocolExists(string protocolName, string protocolVersion);
		}

		/// <summary>
		/// Contains methods for input validation.
		/// </summary>
		internal static class InputValidator
		{
			/// <summary>
			/// Validates the name of an element, service, redundancy group, template or folder.
			/// </summary>
			/// <param name = "name">The element name.</param>
			/// <param name = "parameterName">The name of the parameter that is passing the name.</param>
			/// <exception cref = "ArgumentNullException">The value of a set operation is <see langword = "null"/>.</exception>
			/// <exception cref = "ArgumentException">The value of a set operation is empty or white space.</exception>
			/// <exception cref = "ArgumentException">The value of a set operation exceeds 200 characters.</exception>
			/// <exception cref = "ArgumentException">The value of a set operation contains a forbidden character.</exception>
			/// <exception cref = "ArgumentException">The value of a set operation contains more than one '%' character.</exception>
			/// <returns><c>true</c> if the name is valid; otherwise, <c>false</c>.</returns>
			/// <remarks>Forbidden characters: '\', '/', ':', '*', '?', '"', '&lt;', '&gt;', '|', '�', ';'.</remarks>
			public static string ValidateName(string name, string parameterName)
			{
				if (name == null)
				{
					throw new System.ArgumentNullException("name");
				}

				if (parameterName == null)
				{
					throw new System.ArgumentNullException("parameterName");
				}

				if (System.String.IsNullOrWhiteSpace(name))
				{
					throw new System.ArgumentException("The name must not be null or white space.", parameterName);
				}

				string trimmedName = name.Trim();
				if (trimmedName.Length > 200)
				{
					throw new System.ArgumentException("The name must not exceed 200 characters.", parameterName);
				}

				// White space is trimmed.
				if (trimmedName[0].Equals('.'))
				{
					throw new System.ArgumentException("The name must not start with a dot ('.').", parameterName);
				}

				if (trimmedName[trimmedName.Length - 1].Equals('.'))
				{
					throw new System.ArgumentException("The name must not end with a dot ('.').", parameterName);
				}

				if (!System.Text.RegularExpressions.Regex.IsMatch(trimmedName, @"^[^/\\:;\*\?<>\|�""]+$"))
				{
					throw new System.ArgumentException("The name contains a forbidden character.", parameterName);
				}

				if (System.Linq.Enumerable.Count(trimmedName, x => x == '%') > 1)
				{
					throw new System.ArgumentException("The name must not contain more than one '%' characters.", parameterName);
				}

				return trimmedName;
			}
		}

		/// <summary>
		/// Updateable interface.
		/// </summary>
		public interface IUpdateable
		{
		}

		/// <summary>
		/// Represents a DataMiner Agent.
		/// </summary>
		internal class Dma : Skyline.DataMiner.Library.Common.DmsObject, Skyline.DataMiner.Library.Common.IDma
		{
			/// <summary>
			/// The object used for DMS communication.
			/// </summary>
			private new readonly Skyline.DataMiner.Library.Common.IDms dms;
			/// <summary>
			/// The DataMiner Agent ID.
			/// </summary>
			private readonly int id;
			private string hostName;
			private string name;
			private Skyline.DataMiner.Library.Common.IDmsScheduler scheduler;
			private string versionInfo;
			/// <summary>
			/// Initializes a new instance of the <see cref = "Dma"/> class.
			/// </summary>
			/// <param name = "dms">The DataMiner System.</param>
			/// <param name = "id">The ID of the DataMiner Agent.</param>
			/// <exception cref = "ArgumentNullException">The <see cref = "IDms"/> reference is <see langword = "null"/>.</exception>
			/// <exception cref = "ArgumentException">The DataMiner Agent ID is negative.</exception>
			internal Dma(Skyline.DataMiner.Library.Common.IDms dms, int id) : base(dms)
			{
				if (id < 1)
				{
					throw new System.ArgumentException(System.String.Format(System.Globalization.CultureInfo.InvariantCulture, "Invalid DataMiner agent ID: {0}", id), "id");
				}

				this.dms = dms;
				this.id = id;
			}

			internal Dma(Skyline.DataMiner.Library.Common.IDms dms, Skyline.DataMiner.Net.Messages.GetDataMinerInfoResponseMessage infoMessage) : base(dms)
			{
				if (infoMessage == null)
				{
					throw new System.ArgumentNullException("infoMessage");
				}

				Parse(infoMessage);
			}

			/// <summary>
			/// Gets the ID of this DataMiner Agent.
			/// </summary>
			/// <value>The ID of this DataMiner Agent.</value>
			public int Id
			{
				get
				{
					return id;
				}
			}

			/// <summary>
			/// Determines whether this DataMiner Agent exists in the DataMiner System.
			/// </summary>
			/// <returns><c>true</c> if the DataMiner Agent exists in the DataMiner System; otherwise, <c>false</c>.</returns>
			public override bool Exists()
			{
				return dms.AgentExists(id);
			}

			/// <summary>
			/// Returns a string that represents the current object.
			/// </summary>
			/// <returns>A string that represents the current object.</returns>
			public override string ToString()
			{
				return System.String.Format(System.Globalization.CultureInfo.InvariantCulture, "DataMiner agent ID: {0}", id);
			}

			internal override void Load()
			{
				try
				{
					Skyline.DataMiner.Net.Messages.GetDataMinerByIDMessage message = new Skyline.DataMiner.Net.Messages.GetDataMinerByIDMessage(id);
					Skyline.DataMiner.Net.Messages.GetDataMinerInfoResponseMessage infoResponseMessage = Dms.Communication.SendSingleResponseMessage(message) as Skyline.DataMiner.Net.Messages.GetDataMinerInfoResponseMessage;
					if (infoResponseMessage != null)
					{
						Parse(infoResponseMessage);
					}
					else
					{
						throw new Skyline.DataMiner.Library.Common.AgentNotFoundException(id);
					}

					Skyline.DataMiner.Net.Messages.GetAgentBuildInfo buildInfoMessage = new Skyline.DataMiner.Net.Messages.GetAgentBuildInfo(id);
					Skyline.DataMiner.Net.Messages.BuildInfoResponse buildInfoResponse = (Skyline.DataMiner.Net.Messages.BuildInfoResponse)Dms.Communication.SendSingleResponseMessage(buildInfoMessage);
					if (buildInfoResponse != null)
					{
						Parse(buildInfoResponse);
					}

					Skyline.DataMiner.Net.Messages.RSAPublicKeyRequest rsapkr;
					rsapkr = new Skyline.DataMiner.Net.Messages.RSAPublicKeyRequest(id)
					{ HostingDataMinerID = id };
					Skyline.DataMiner.Net.Messages.RSAPublicKeyResponse resp = Dms.Communication.SendSingleResponseMessage(rsapkr) as Skyline.DataMiner.Net.Messages.RSAPublicKeyResponse;
					Skyline.DataMiner.Library.Common.RSA.PublicKey = new System.Security.Cryptography.RSAParameters { Modulus = resp.Modulus, Exponent = resp.Exponent };
					scheduler = new Skyline.DataMiner.Library.Common.DmsScheduler(this);
					IsLoaded = true;
				}
				catch (Skyline.DataMiner.Net.Exceptions.DataMinerException e)
				{
					if (e.ErrorCode == -2146233088)
					{
						// 0x80131500, No agent available with ID.
						throw new Skyline.DataMiner.Library.Common.AgentNotFoundException(id);
					}
					else
					{
						throw;
					}
				}
			}

			private void Parse(Skyline.DataMiner.Net.Messages.GetDataMinerInfoResponseMessage infoMessage)
			{
				name = infoMessage.AgentName;
				hostName = infoMessage.ComputerName;
			}

			/// <summary>
			/// Parses the version information of the DataMiner Agent.
			/// </summary>
			/// <param name = "response">The response message.</param>
			private void Parse(Skyline.DataMiner.Net.Messages.BuildInfoResponse response)
			{
				if (response == null || response.Agents == null || response.Agents.Length == 0)
				{
					throw new System.ArgumentException("Agent build information cannot be null or empty");
				}

				string rawVersion = response.Agents[0].RawVersion;
				this.versionInfo = rawVersion;
			}
		}

		/// <summary>
		/// DataMiner Agent interface.
		/// </summary>
		public interface IDma
		{
			/// <summary>
			/// Gets the DataMiner System this Agent is part of.
			/// </summary>
			/// <value>The DataMiner system this Agent is part of.</value>
			Skyline.DataMiner.Library.Common.IDms Dms
			{
				get;
			}

			/// <summary>
			/// Gets the ID of this DataMiner Agent.
			/// </summary>
			/// <value>The ID of this DataMiner Agent.</value>
			int Id
			{
				get;
			}

			/// <summary>
			/// Determines whether this DataMiner Agent exists in the DataMiner System.
			/// </summary>
			/// <returns><c>true</c> if the DataMiner Agent exists in the DataMiner System; otherwise, <c>false</c>.</returns>
			bool Exists();
		}

		/// <summary>
		/// Represents a communication interface implementation using the <see cref = "IConnection"/> interface.
		/// </summary>
		internal class ConnectionCommunication : Skyline.DataMiner.Library.Common.ICommunication
		{
			/// <summary>
			/// The SLNet connection.
			/// </summary>
			private readonly Skyline.DataMiner.Net.IConnection connection;
			/// <summary>
			/// Initializes a new instance of the <see cref = "ConnectionCommunication"/> class using an instance of the <see cref = "IConnection"/> class.
			/// </summary>
			/// <param name = "connection">The connection.</param>
			/// <exception cref = "ArgumentNullException"><paramref name = "connection"/> is <see langword = "null"/>.</exception>
			public ConnectionCommunication(Skyline.DataMiner.Net.IConnection connection)
			{
				if (connection == null)
				{
					throw new System.ArgumentNullException("connection");
				}

				this.connection = connection;
			}

			/// <summary>
			/// Sends a message to the SLNet process.
			/// </summary>
			/// <param name = "message">The message to be sent.</param>
			/// <exception cref = "ArgumentNullException"><paramref name = "message"/> is <see langword = "null"/>.</exception>
			/// <returns>The message responses.</returns>
			public Skyline.DataMiner.Net.Messages.DMSMessage[] SendMessage(Skyline.DataMiner.Net.Messages.DMSMessage message)
			{
				if (message == null)
				{
					throw new System.ArgumentNullException("message");
				}

				return connection.HandleMessage(message);
			}

			/// <summary>
			/// Sends a message to the SLNet process.
			/// </summary>
			/// <param name = "message">The message to be sent.</param>
			/// <exception cref = "ArgumentNullException"><paramref name = "message"/> is <see langword = "null"/>.</exception>
			/// <returns>The message response.</returns>
			public Skyline.DataMiner.Net.Messages.DMSMessage SendSingleResponseMessage(Skyline.DataMiner.Net.Messages.DMSMessage message)
			{
				if (message == null)
				{
					throw new System.ArgumentNullException("message");
				}

				return connection.HandleSingleResponseMessage(message);
			}
		}

		/// <summary>
		/// Defines methods to send messages to a DataMiner System.
		/// </summary>
		public interface ICommunication
		{
			/// <summary>
			/// Sends a message to the SLNet process that can have multiple responses.
			/// </summary>
			/// <param name = "message">The message to be sent.</param>
			/// <exception cref = "ArgumentNullException">The message cannot be null.</exception>
			/// <returns>The message responses.</returns>
			Skyline.DataMiner.Net.Messages.DMSMessage[] SendMessage(Skyline.DataMiner.Net.Messages.DMSMessage message);
			/// <summary>
			/// Sends a message to the SLNet process that returns a single response.
			/// </summary>
			/// <param name = "message">The message to be sent.</param>
			/// <exception cref = "ArgumentNullException"><paramref name = "message"/> is <see langword = "null"/>.</exception>
			/// <returns>The message response.</returns>
			Skyline.DataMiner.Net.Messages.DMSMessage SendSingleResponseMessage(Skyline.DataMiner.Net.Messages.DMSMessage message);
		}

		/// <summary>
		/// A collection of IElementConnection objects.
		/// </summary>
		public class ElementConnectionCollection : Skyline.DataMiner.Library.Common.IElementConnectionCollection
		{
			private readonly Skyline.DataMiner.Library.Common.IElementConnection[] connections;
			private readonly bool canBeValidated;
			private readonly System.Collections.Generic.IList<Skyline.DataMiner.Library.Common.IDmsConnectionInfo> protocolConnectionInfo;
			/// <summary>
			/// initiates a new instance.
			/// </summary>
			/// <param name = "protocolConnectionInfo"></param>
			internal ElementConnectionCollection(System.Collections.Generic.IList<Skyline.DataMiner.Library.Common.IDmsConnectionInfo> protocolConnectionInfo)
			{
				int amountOfConnections = protocolConnectionInfo.Count;
				this.connections = new Skyline.DataMiner.Library.Common.IElementConnection[amountOfConnections];
				this.protocolConnectionInfo = protocolConnectionInfo;
				canBeValidated = true;
			}

			/// <summary>
			/// Initiates a new instance.
			/// </summary>
			/// <param name = "message"></param>
			internal ElementConnectionCollection(Skyline.DataMiner.Net.Messages.ElementInfoEventMessage message)
			{
				int amountOfConnections = 1;
				if (message != null && message.ExtraPorts != null)
				{
					amountOfConnections += message.ExtraPorts.Length;
				}

				this.connections = new Skyline.DataMiner.Library.Common.IElementConnection[amountOfConnections];
				canBeValidated = false;
			}

			/// <summary>
			/// Returns an enumerator that iterates through the collection.
			/// </summary>
			/// <returns>An enumerator that can be used to iterate through the collection.</returns>
			public System.Collections.Generic.IEnumerator<Skyline.DataMiner.Library.Common.IElementConnection> GetEnumerator()
			{
				return ((System.Collections.Generic.IEnumerable<Skyline.DataMiner.Library.Common.IElementConnection>)connections).GetEnumerator();
			}

			/// <summary>
			/// Returns an enumerator that iterates through a collection.
			/// </summary>
			/// <returns>An <see cref = "IEnumerator"/> object that can be used to iterate through the collection.</returns>
			System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
			{
				return this.GetEnumerator();
			}

			/// <summary>
			/// Gets or sets an entry in the collection.
			/// </summary>
			/// <param name = "index"></param>
			/// <returns></returns>
			public Skyline.DataMiner.Library.Common.IElementConnection this[int index]
			{
				get
				{
					return connections[index];
				}

				set
				{
					bool valid = ValidateConnectionTypeAtPos(index, value);
					if (valid)
					{
						connections[index] = value;
					}
					else
					{
						throw new Skyline.DataMiner.Library.Common.IncorrectDataException("Invalid connection type provided at index " + index);
					}
				}
			}

			/// <summary>
			/// Validates the provided <see cref = "IElementConnection"/> against the provided Protocol.
			/// </summary>
			/// <param name = "index">The index position of the connection to validate.</param>
			/// <param name = "conn">The IElementConnection connection.</param>
			/// <exception cref = "ArgumentOutOfRangeException"><paramref name = "index"/> is out of range.</exception>
			/// <returns></returns>
			private bool ValidateConnectionTypeAtPos(int index, Skyline.DataMiner.Library.Common.IElementConnection conn)
			{
				if (!canBeValidated)
				{
					return true;
				}

				if (index < 0 || ((index + 1) > protocolConnectionInfo.Count))
				{
					throw new System.ArgumentOutOfRangeException("index", "Index needs to be between 0 and the amount of connections in the protocol minus 1.");
				}

				return ValidateConnectionInfo(conn, protocolConnectionInfo[index]);
			}

			/// <summary>
			/// Validates a single connection.
			/// </summary>
			/// <param name = "conn"><see cref = "IElementConnection"/> object.</param>
			/// <param name = "connectionInfo"><see cref = "IDmsConnectionInfo"/> object.</param>
			/// <exception cref = "ArgumentNullException"><paramref name = "conn"/> is <see langword = "null"/>.</exception>
			/// <exception cref = "ArgumentNullException"><paramref name = "connectionInfo"/> is <see langword = "null"/>.</exception>
			/// <returns></returns>
			private static bool ValidateConnectionInfo(Skyline.DataMiner.Library.Common.IElementConnection conn, Skyline.DataMiner.Library.Common.IDmsConnectionInfo connectionInfo)
			{
				if (conn == null)
				{
					throw new Skyline.DataMiner.Library.Common.IncorrectDataException("conn: Invalid data , ElementConfiguration does not contain connection info");
				}

				if (connectionInfo == null)
				{
					throw new Skyline.DataMiner.Library.Common.IncorrectDataException("connectionInfo: Invalid data , Protocol does not contain connection info");
				}

				switch (connectionInfo.Type)
				{
					case Skyline.DataMiner.Library.Common.ConnectionType.SnmpV1:
						return ValidateAsSnmpV1(conn);
					case Skyline.DataMiner.Library.Common.ConnectionType.SnmpV2:
						return ValidateAsSnmpV2(conn);
					case Skyline.DataMiner.Library.Common.ConnectionType.SnmpV3:
						return ValidateAsSnmpV3(conn);
					case Skyline.DataMiner.Library.Common.ConnectionType.Virtual:
						return conn is Skyline.DataMiner.Library.Common.IVirtualConnection;
					case Skyline.DataMiner.Library.Common.ConnectionType.Http:
						return conn is Skyline.DataMiner.Library.Common.IHttpConnection;
					default:
						return false;
				}
			}

			/// <summary>
			/// Validate a connection for SNMPv1
			/// </summary>
			/// <param name = "conn">object of type <see cref = "IElementConnection"/> to validate.</param>
			/// <returns></returns>
			private static bool ValidateAsSnmpV1(Skyline.DataMiner.Library.Common.IElementConnection conn)
			{
				return conn is Skyline.DataMiner.Library.Common.ISnmpV1Connection || conn is Skyline.DataMiner.Library.Common.ISnmpV2Connection || conn is Skyline.DataMiner.Library.Common.ISnmpV3Connection;
			}

			/// <summary>
			/// Validate a connection for SNMPv2
			/// </summary>
			/// <param name = "conn">object of type <see cref = "IElementConnection"/> to validate.</param>
			/// <returns></returns>
			private static bool ValidateAsSnmpV2(Skyline.DataMiner.Library.Common.IElementConnection conn)
			{
				return conn is Skyline.DataMiner.Library.Common.ISnmpV2Connection || conn is Skyline.DataMiner.Library.Common.ISnmpV3Connection;
			}

			/// <summary>
			/// Validate a connection for SNMPv3
			/// </summary>
			/// <param name = "conn">object of type <see cref = "IElementConnection"/> to validate.</param>
			/// <returns></returns>
			private static bool ValidateAsSnmpV3(Skyline.DataMiner.Library.Common.IElementConnection conn)
			{
				return conn is Skyline.DataMiner.Library.Common.ISnmpV3Connection || conn is Skyline.DataMiner.Library.Common.ISnmpV2Connection;
			}
		}

		/// <summary>
		/// A collection of IElementConnection objects.
		/// </summary>
		public interface IElementConnectionCollection : System.Collections.Generic.IEnumerable<Skyline.DataMiner.Library.Common.IElementConnection>
		{
			/// <summary>
			/// Gets or sets an entry in the collection.
			/// </summary>
			Skyline.DataMiner.Library.Common.IElementConnection this[int index]
			{
				get;
				set;
			}
		}

		/// <summary>
		/// Represents information about a connection.
		/// </summary>
		internal class DmsConnectionInfo : Skyline.DataMiner.Library.Common.IDmsConnectionInfo
		{
			/// <summary>
			/// The name of the connection.
			/// </summary>
			private readonly string name;
			/// <summary>
			/// The connection type.
			/// </summary>
			private readonly Skyline.DataMiner.Library.Common.ConnectionType type;
			/// <summary>
			/// Initializes a new instance of the <see cref = "DmsConnectionInfo"/> class.
			/// </summary>
			/// <param name = "name">The connection name.</param>
			/// <param name = "type">The connection type.</param>
			internal DmsConnectionInfo(string name, Skyline.DataMiner.Library.Common.ConnectionType type)
			{
				this.name = name;
				this.type = type;
			}

			/// <summary>
			/// Gets the connection type.
			/// </summary>
			/// <value>The connection type.</value>
			public Skyline.DataMiner.Library.Common.ConnectionType Type
			{
				get
				{
					return type;
				}
			}

			/// <summary>
			/// Returns a string that represents the current object.
			/// </summary>
			/// <returns>A string that represents the current object.</returns>
			public override string ToString()
			{
				return System.String.Format(System.Globalization.CultureInfo.InvariantCulture, "Connection with Name:{0} and Type:{1}.", name, type);
			}
		}

		/// <summary>
		/// DataMiner element connection information interface.
		/// </summary>
		public interface IDmsConnectionInfo
		{
			/// <summary>
			/// Gets the connection type.
			/// </summary>
			/// <value>The connection type.</value>
			Skyline.DataMiner.Library.Common.ConnectionType Type
			{
				get;
			}
		}

		/// <summary>
		/// Specifies the connection type.
		/// </summary>
		public enum ConnectionType
		{
			/// <summary>
			/// Undefined connection type.
			/// </summary>
			Undefined = 0,
			/// <summary>
			/// SNMPv1 connection.
			/// </summary>
			SnmpV1 = 1,
			/// <summary>
			/// Serial connection.
			/// </summary>
			Serial = 2,
			/// <summary>
			/// Smart-serial connection.
			/// </summary>
			SmartSerial = 3,
			/// <summary>
			/// Virtual connection.
			/// </summary>
			Virtual = 4,
			/// <summary>
			/// GBIP (General Purpose Interface Bus) connection.
			/// </summary>
			Gpib = 5,
			/// <summary>
			/// OPC (OLE for Process Control) connection.
			/// </summary>
			Opc = 6,
			/// <summary>
			/// SLA (Service Level Agreement).
			/// </summary>
			Sla = 7,
			/// <summary>
			/// SNMPv2 connection.
			/// </summary>
			SnmpV2 = 8,
			/// <summary>
			/// SNMPv3 connection.
			/// </summary>
			SnmpV3 = 9,
			/// <summary>
			/// HTTP connection.
			/// </summary>
			Http = 10,
			/// <summary>
			/// Service.
			/// </summary>
			Service = 11,
			/// <summary>
			/// Serial single connection.
			/// </summary>
			SerialSingle = 12,
			/// <summary>
			/// Smart-serial single connection.
			/// </summary>
			SmartSerialSingle = 13,
			/// <summary>
			/// Web Socket connection.
			/// </summary>
			WebSocket = 14
		}

		/// <summary>
		/// Specifies the state of the element.
		/// </summary>
		public enum ElementState
		{
			/// <summary>
			/// Specifies the undefined element state.
			/// </summary>
			Undefined = 0,
			/// <summary>
			/// Specifies the active element state.
			/// </summary>
			Active = 1,
			/// <summary>
			/// Specifies the hidden element state.
			/// </summary>
			Hidden = 2,
			/// <summary>
			/// Specifies the paused element state.
			/// </summary>
			Paused = 3,
			/// <summary>
			/// Specifies the stopped element state.
			/// </summary>
			Stopped = 4,
			/// <summary>
			/// Specifies the deleted element state.
			/// </summary>
			Deleted = 6,
			/// <summary>
			/// Specifies the error element state.
			/// </summary>
			Error = 10,
			/// <summary>
			/// Specifies the restart element state.
			/// </summary>
			Restart = 11,
			/// <summary>
			/// Specifies the masked element state.
			/// </summary>
			Masked = 12
		}

		/// <summary>
		/// Specifies the protocol type.
		/// </summary>
		public enum ProtocolType
		{
			/// <summary>
			/// Undefined protocol type.
			/// </summary>
			Undefined = 0,
			/// <summary>
			/// The SNMP protocol type.
			/// </summary>
			Snmp = 1,
			/// <summary>
			/// The SNMPv1 protocol type.
			/// </summary>
			SnmpV1 = 1,
			/// <summary>
			/// The serial protocol type.
			/// </summary>
			Serial = 2,
			/// <summary>
			/// The smart serial protocol type.
			/// </summary>
			SmartSerial = 3,
			/// <summary>
			/// The virtual protocol type.
			/// </summary>
			Virtual = 4,
			/// <summary>
			/// The General Purpose Interface Bus (GPIB) protocol type.
			/// </summary>
			Gpib = 5,
			/// <summary>
			/// The OLE Process Controller (OPC) protocol type.
			/// </summary>
			Opc = 6,
			/// <summary>
			/// The Service Level Agreement (SLA) protocol type.
			/// </summary>
			Sla = 7,
			/// <summary>
			/// The SNMPv2 protocol type.
			/// </summary>
			SnmpV2 = 8,
			/// <summary>
			/// The SNMPv3 protocol type.
			/// </summary>
			SnmpV3 = 9,
			/// <summary>
			/// The HTTP protocol type.
			/// </summary>
			Http = 10,
			/// <summary>
			/// The service protocol type.
			/// </summary>
			Service = 11,
			/// <summary>
			/// The serial single protocol type.
			/// </summary>
			SerialSingle = 12,
			/// <summary>
			/// The smart serial single protocol type.
			/// </summary>
			SmartSerialSingle = 13,
			/// <summary>
			/// The smart serial raw protocol type.
			/// </summary>
			SmartSerialRaw = 14,
			/// <summary>
			/// The smart serial raw single protocol type.
			/// </summary>
			SmartSerialRawSingle = 15,
			/// <summary>
			/// The websocket protocol type.
			/// </summary>
			WebSocket = 16
		}

		/// <summary>
		/// The exception that is thrown when an action is performed on a DataMiner Agent that was not found.
		/// </summary>
		[System.Serializable]
		[Skyline.DataMiner.Library.Common.Attributes.DllImport("System.Runtime.Serialization.dll")]
		public class AgentNotFoundException : Skyline.DataMiner.Library.Common.DmsException
		{
			/// <summary>
			/// Initializes a new instance of the <see cref = "AgentNotFoundException"/> class.
			/// </summary>
			public AgentNotFoundException()
			{
			}

			/// <summary>
			/// Initializes a new instance of the <see cref = "AgentNotFoundException"/> class with a specified DataMiner Agent ID.
			/// </summary>
			/// <param name = "id">The ID of the DataMiner Agent that was not found.</param>
			public AgentNotFoundException(int id) : base(System.String.Format(System.Globalization.CultureInfo.InvariantCulture, "The agent with ID '{0}' was not found.", id))
			{
			}

			/// <summary>
			/// Initializes a new instance of the <see cref = "AgentNotFoundException"/> class with a specified error message.
			/// </summary>
			/// <param name = "message">The error message that explains the reason for the exception.</param>
			public AgentNotFoundException(string message) : base(message)
			{
			}

			/// <summary>
			/// Initializes a new instance of the <see cref = "AgentNotFoundException"/> class with a specified error message and a reference to the inner exception that is the cause of this exception.
			/// </summary>
			/// <param name = "message">The error message that explains the reason for the exception.</param>
			/// <param name = "innerException">The exception that is the cause of the current exception, or a null reference if no inner exception is specified.</param>
			public AgentNotFoundException(string message, System.Exception innerException) : base(message, innerException)
			{
			}

			/// <summary>
			/// Initializes a new instance of the <see cref = "AgentNotFoundException"/> class with serialized data.
			/// </summary>
			/// <param name = "info">The serialization info.</param>
			/// <param name = "context">The streaming context.</param>
			/// <exception cref = "ArgumentNullException">The <paramref name = "info"/> parameter is <see langword = "null"/>.</exception>
			/// <exception cref = "SerializationException">The class name is <see langword = "null"/> or HResult is zero (0).</exception>
			/// <remarks>This constructor is called during deserialization to reconstitute the exception object transmitted over a stream.</remarks>
			protected AgentNotFoundException(System.Runtime.Serialization.SerializationInfo info, System.Runtime.Serialization.StreamingContext context) : base(info, context)
			{
			}
		}

		/// <summary>
		/// The exception that is thrown when a requested alarm template was not found.
		/// </summary>
		[System.Serializable]
		[Skyline.DataMiner.Library.Common.Attributes.DllImport("System.Runtime.Serialization.dll")]
		public class AlarmTemplateNotFoundException : Skyline.DataMiner.Library.Common.TemplateNotFoundException
		{
			/// <summary>
			/// Initializes a new instance of the <see cref = "AlarmTemplateNotFoundException"/> class.
			/// </summary>
			public AlarmTemplateNotFoundException()
			{
			}

			/// <summary>
			/// Initializes a new instance of the <see cref = "AlarmTemplateNotFoundException"/> class.
			/// </summary>
			/// <param name = "message">The error message that explains the reason for the exception.</param>
			public AlarmTemplateNotFoundException(string message) : base(message)
			{
			}

			/// <summary>
			/// Initializes a new instance of the <see cref = "AlarmTemplateNotFoundException"/> class with a specified error message and a reference to the inner exception that is the cause of this exception.
			/// </summary>
			/// <param name = "message">The error message that explains the reason for the exception.</param>
			/// <param name = "innerException">The exception that is the cause of the current exception, or a null reference if no inner exception is specified.</param>
			public AlarmTemplateNotFoundException(string message, System.Exception innerException) : base(message, innerException)
			{
			}

			/// <summary>
			/// Initializes a new instance of the <see cref = "AlarmTemplateNotFoundException"/> class.
			/// </summary>
			/// <param name = "templateName">The name of the template.</param>
			/// <param name = "protocol">The protocol this template relates to.</param>
			public AlarmTemplateNotFoundException(string templateName, Skyline.DataMiner.Library.Common.IDmsProtocol protocol) : base(templateName, protocol)
			{
			}

			/// <summary>
			/// Initializes a new instance of the <see cref = "AlarmTemplateNotFoundException"/> class.
			/// </summary>
			/// <param name = "templateName">The name of the template.</param>
			/// <param name = "protocolName">The name of the protocol.</param>
			/// <param name = "protocolVersion">The version of the protocol.</param>
			public AlarmTemplateNotFoundException(string templateName, string protocolName, string protocolVersion) : base(templateName, protocolName, protocolVersion)
			{
			}

			/// <summary>
			/// Initializes a new instance of the <see cref = "AlarmTemplateNotFoundException"/> class with serialized data.
			/// </summary>
			/// <param name = "info">The serialization info.</param>
			/// <param name = "context">The streaming context.</param>
			/// <exception cref = "ArgumentNullException">The <paramref name = "info"/> parameter is <see langword = "null"/>.</exception>
			/// <exception cref = "SerializationException">The class name is <see langword = "null"/> or HResult is zero (0).</exception>
			/// <remarks>This constructor is called during deserialization to reconstitute the exception object transmitted over a stream.</remarks>
			protected AlarmTemplateNotFoundException(System.Runtime.Serialization.SerializationInfo info, System.Runtime.Serialization.StreamingContext context) : base(info, context)
			{
			}
		}

		/// <summary>
		/// The exception that is thrown when an exception occurs in a DataMiner System.
		/// </summary>
		[System.Serializable]
		[Skyline.DataMiner.Library.Common.Attributes.DllImport("System.Runtime.Serialization.dll")]
		public class DmsException : System.Exception
		{
			/// <summary>
			/// Initializes a new instance of the <see cref = "DmsException"/> class.
			/// </summary>
			public DmsException()
			{
			}

			/// <summary>
			/// Initializes a new instance of the <see cref = "DmsException"/> class.
			/// </summary>
			/// <param name = "message">The error message that explains the reason for the exception.</param>
			public DmsException(string message) : base(message)
			{
			}

			/// <summary>
			/// Initializes a new instance of the <see cref = "DmsException"/> class with a specified error message and a reference to the inner exception that is the cause of this exception.
			/// </summary>
			/// <param name = "message">The error message that explains the reason for the exception.</param>
			/// <param name = "innerException">The exception that is the cause of the current exception, or a null reference if no inner exception is specified.</param>
			public DmsException(string message, System.Exception innerException) : base(message, innerException)
			{
			}

			/// <summary>
			/// Initializes a new instance of the <see cref = "DmsException"/> class with serialized data.
			/// </summary>
			/// <param name = "info">The serialization info.</param>
			/// <param name = "context">The streaming context.</param>
			/// <exception cref = "ArgumentNullException">The <paramref name = "info"/> parameter is <see langword = "null"/>.</exception>
			/// <exception cref = "SerializationException">The class name is <see langword = "null"/> or HResult is zero (0).</exception>
			/// <remarks>This constructor is called during deserialization to reconstitute the exception object transmitted over a stream.</remarks>
			protected DmsException(System.Runtime.Serialization.SerializationInfo info, System.Runtime.Serialization.StreamingContext context) : base(info, context)
			{
			}
		}

		/// <summary>
		/// The exception that is thrown when performing actions on an element that was not found.
		/// </summary>
		[System.Serializable]
		[Skyline.DataMiner.Library.Common.Attributes.DllImport("System.Runtime.Serialization.dll")]
		public class ElementNotFoundException : Skyline.DataMiner.Library.Common.DmsException
		{
			/// <summary>
			/// Initializes a new instance of the <see cref = "ElementNotFoundException"/> class.
			/// </summary>
			public ElementNotFoundException()
			{
			}

			/// <summary>
			/// Initializes a new instance of the <see cref = "ElementNotFoundException"/> class.
			/// </summary>
			/// <param name = "dmsElementId">The DataMiner Agent ID/element ID of the element that was not found.</param>
			public ElementNotFoundException(Skyline.DataMiner.Library.Common.DmsElementId dmsElementId) : base(System.String.Format(System.Globalization.CultureInfo.InvariantCulture, "Element with DMA ID '{0}' and element ID '{1}' was not found.", dmsElementId.AgentId, dmsElementId.ElementId))
			{
			}

			/// <summary>
			/// Initializes a new instance of the <see cref = "ElementNotFoundException"/> class.
			/// </summary>
			/// <param name = "dmaId">The ID of the DataMiner Agent that was not found.</param>
			/// <param name = "elementId">The ID of the element that was not found.</param>
			public ElementNotFoundException(int dmaId, int elementId) : base(System.String.Format(System.Globalization.CultureInfo.InvariantCulture, "Element with DMA ID '{0}' and element ID '{1}' was not found.", dmaId, elementId))
			{
			}

			/// <summary>
			/// Initializes a new instance of the <see cref = "ElementNotFoundException"/> class.
			/// </summary>
			/// <param name = "message">The error message that explains the reason for the exception.</param>
			public ElementNotFoundException(string message) : base(message)
			{
			}

			/// <summary>
			/// Initializes a new instance of the <see cref = "ElementNotFoundException"/> class with a specified error message and a reference to the inner exception that is the cause of this exception.
			/// </summary>
			/// <param name = "message">The error message that explains the reason for the exception.</param>
			/// <param name = "innerException">The exception that is the cause of the current exception, or a null reference if no inner exception is specified.</param>
			public ElementNotFoundException(string message, System.Exception innerException) : base(message, innerException)
			{
			}

			/// <summary>
			/// Initializes a new instance of the <see cref = "ElementNotFoundException"/> class with a specified error message and a reference to the inner exception that is the cause of this exception.
			/// </summary>
			/// <param name = "dmsElementId">The DataMiner Agent ID/element ID of the element that was not found.</param>
			/// <param name = "innerException">The exception that is the cause of the current exception, or a null reference if no inner exception is specified.</param>
			public ElementNotFoundException(Skyline.DataMiner.Library.Common.DmsElementId dmsElementId, System.Exception innerException) : base(System.String.Format(System.Globalization.CultureInfo.InvariantCulture, "Element with DMA ID '{0}' and element ID '{1}' was not found.", dmsElementId.AgentId, dmsElementId.ElementId), innerException)
			{
			}

			/// <summary>
			/// Initializes a new instance of the <see cref = "ElementNotFoundException"/> class with serialized data.
			/// </summary>
			/// <param name = "info">The serialization info.</param>
			/// <param name = "context">The streaming context.</param>
			/// <exception cref = "ArgumentNullException">The <paramref name = "info"/> parameter is <see langword = "null"/>.</exception>
			/// <exception cref = "SerializationException">The class name is <see langword = "null"/> or HResult is zero (0).</exception>
			/// <remarks>This constructor is called during deserialization to reconstitute the exception object transmitted over a stream.</remarks>
			protected ElementNotFoundException(System.Runtime.Serialization.SerializationInfo info, System.Runtime.Serialization.StreamingContext context) : base(info, context)
			{
			}
		}

		/// <summary>
		/// The exception that is thrown when invalid data was provided.
		/// </summary>
		[System.Serializable]
		[Skyline.DataMiner.Library.Common.Attributes.DllImport("System.Runtime.Serialization.dll")]
		public class IncorrectDataException : Skyline.DataMiner.Library.Common.DmsException
		{
			/// <summary>
			/// Initializes a new instance of the <see cref = "IncorrectDataException"/> class.
			/// </summary>
			public IncorrectDataException()
			{
			}

			/// <summary>
			/// Initializes a new instance of the <see cref = "IncorrectDataException"/> class.
			/// </summary>
			/// <param name = "message">The error message that explains the reason for the exception.</param>
			public IncorrectDataException(string message) : base(message)
			{
			}

			/// <summary>
			/// Initializes a new instance of the <see cref = "IncorrectDataException"/> class with a specified error message and a reference to the inner exception that is the cause of this exception.
			/// </summary>
			/// <param name = "message">The error message that explains the reason for the exception.</param>
			/// <param name = "innerException">The exception that is the cause of the current exception, or a null reference if no inner exception is specified.</param>
			public IncorrectDataException(string message, System.Exception innerException) : base(message, innerException)
			{
			}

			/// <summary>
			/// Initializes a new instance of the <see cref = "IncorrectDataException"/> class with serialized data.
			/// </summary>
			/// <param name = "info">The serialization info.</param>
			/// <param name = "context">The streaming context.</param>
			/// <exception cref = "ArgumentNullException">The <paramref name = "info"/> parameter is <see langword = "null"/>.</exception>
			/// <exception cref = "SerializationException">The class name is <see langword = "null"/> or HResult is zero (0).</exception>
			/// <remarks>This constructor is called during deserialization to reconstitute the exception object transmitted over a stream.</remarks>
			protected IncorrectDataException(System.Runtime.Serialization.SerializationInfo info, System.Runtime.Serialization.StreamingContext context) : base(info, context)
			{
			}
		}

		/// <summary>
		/// The exception that is thrown when a requested protocol was not found.
		/// </summary>
		[System.Serializable]
		[Skyline.DataMiner.Library.Common.Attributes.DllImport("System.Runtime.Serialization.dll")]
		public class ProtocolNotFoundException : Skyline.DataMiner.Library.Common.DmsException
		{
			/// <summary>
			/// Initializes a new instance of the <see cref = "ProtocolNotFoundException"/> class.
			/// </summary>
			public ProtocolNotFoundException()
			{
			}

			/// <summary>
			/// Initializes a new instance of the <see cref = "ProtocolNotFoundException"/> class.
			/// </summary>
			/// <param name = "protocolName">The name of the protocol.</param>
			/// <param name = "protocolVersion">The version of the protocol.</param>
			public ProtocolNotFoundException(string protocolName, string protocolVersion) : base(System.String.Format(System.Globalization.CultureInfo.InvariantCulture, "Protocol with name '{0}' and version '{1}' was not found.", protocolName, protocolVersion))
			{
			}

			/// <summary>
			/// Initializes a new instance of the <see cref = "ProtocolNotFoundException"/> class.
			/// </summary>
			/// <param name = "message">The error message that explains the reason for the exception.</param>
			public ProtocolNotFoundException(string message) : base(message)
			{
			}

			/// <summary>
			/// Initializes a new instance of the <see cref = "ProtocolNotFoundException"/> class.
			/// </summary>
			/// <param name = "protocolName">The name of the protocol.</param>
			/// <param name = "protocolVersion">The version of the protocol.</param>
			/// <param name = "innerException">The exception that is the cause of the current exception, or a null reference if no inner exception is specified.</param>
			public ProtocolNotFoundException(string protocolName, string protocolVersion, System.Exception innerException) : base(System.String.Format(System.Globalization.CultureInfo.InvariantCulture, "Protocol with name '{0}' and version '{1}' was not found.", protocolName, protocolVersion), innerException)
			{
			}

			/// <summary>
			/// Initializes a new instance of the <see cref = "ProtocolNotFoundException"/> class with a specified error message and a reference to the inner exception that is the cause of this exception.
			/// </summary>
			/// <param name = "message">The error message that explains the reason for the exception.</param>
			/// <param name = "innerException">The exception that is the cause of the current exception, or a null reference if no inner exception is specified.</param>
			public ProtocolNotFoundException(string message, System.Exception innerException) : base(message, innerException)
			{
			}

			/// <summary>
			/// Initializes a new instance of the <see cref = "ProtocolNotFoundException"/> class with serialized data.
			/// </summary>
			/// <param name = "info">The serialization info.</param>
			/// <param name = "context">The streaming context.</param>
			/// <exception cref = "ArgumentNullException">The <paramref name = "info"/> parameter is <see langword = "null"/>.</exception>
			/// <exception cref = "SerializationException">The class name is <see langword = "null"/> or HResult is zero (0).</exception>
			/// <remarks>This constructor is called during deserialization to reconstitute the exception object transmitted over a stream.</remarks>
			protected ProtocolNotFoundException(System.Runtime.Serialization.SerializationInfo info, System.Runtime.Serialization.StreamingContext context) : base(info, context)
			{
			}
		}

		/// <summary>
		/// The exception that is thrown when a requested template was not found.
		/// </summary>
		[System.Serializable]
		[Skyline.DataMiner.Library.Common.Attributes.DllImport("System.Runtime.Serialization.dll")]
		public class TemplateNotFoundException : Skyline.DataMiner.Library.Common.DmsException
		{
			/// <summary>
			/// Initializes a new instance of the <see cref = "TemplateNotFoundException"/> class.
			/// </summary>
			public TemplateNotFoundException()
			{
			}

			/// <summary>
			/// Initializes a new instance of the <see cref = "TemplateNotFoundException"/> class.
			/// </summary>
			/// <param name = "templateName">The name of the template.</param>
			/// <param name = "protocol">The protocol this template relates to.</param>
			/// <exception cref = "ArgumentNullException"><paramref name = "protocol"/> is <see langword = "null"/>.</exception>
			public TemplateNotFoundException(string templateName, Skyline.DataMiner.Library.Common.IDmsProtocol protocol) : base(BuildMessageString(templateName, protocol))
			{
			}

			/// <summary>
			/// Initializes a new instance of the <see cref = "TemplateNotFoundException"/> class.
			/// </summary>
			/// <param name = "templateName">The name of the template.</param>
			/// <param name = "protocolName">The name of the protocol.</param>
			/// <param name = "protocolVersion">The version of the protocol.</param>
			public TemplateNotFoundException(string templateName, string protocolName, string protocolVersion) : base(System.String.Format(System.Globalization.CultureInfo.InvariantCulture, "Template \"{0}\" for protocol \"{1}\" version \"{2}\" was not found.", templateName, protocolName, protocolVersion))
			{
			}

			/// <summary>
			/// Initializes a new instance of the <see cref = "TemplateNotFoundException"/> class.
			/// </summary>
			/// <param name = "message">The error message that explains the reason for the exception.</param>
			public TemplateNotFoundException(string message) : base(message)
			{
			}

			/// <summary>
			/// Initializes a new instance of the <see cref = "TemplateNotFoundException"/> class.
			/// </summary>
			/// <param name = "templateName">The name of the template.</param>
			/// <param name = "protocolName">The name of the protocol.</param>
			/// <param name = "protocolVersion">The version of the protocol.</param>
			/// <param name = "innerException">The exception that is the cause of the current exception, or a null reference if no inner exception is specified.</param>
			public TemplateNotFoundException(string templateName, string protocolName, string protocolVersion, System.Exception innerException) : base(System.String.Format(System.Globalization.CultureInfo.InvariantCulture, "Template \"{0}\" for protocol \"{1}\" version \"{2}\" was not found.", templateName, protocolName, protocolVersion), innerException)
			{
			}

			/// <summary>
			/// Initializes a new instance of the <see cref = "TemplateNotFoundException"/> class with a specified error message and a reference to the inner exception that is the cause of this exception.
			/// </summary>
			/// <param name = "message">The error message that explains the reason for the exception.</param>
			/// <param name = "innerException">The exception that is the cause of the current exception, or a null reference if no inner exception is specified.</param>
			public TemplateNotFoundException(string message, System.Exception innerException) : base(message, innerException)
			{
			}

			/// <summary>
			/// Initializes a new instance of the <see cref = "TemplateNotFoundException"/> class with serialized data.
			/// </summary>
			/// <param name = "info">The serialization info.</param>
			/// <param name = "context">The streaming context.</param>
			/// <exception cref = "ArgumentNullException">The <paramref name = "info"/> parameter is <see langword = "null"/>.</exception>
			/// <exception cref = "SerializationException">The class name is <see langword = "null"/> or HResult is zero (0).</exception>
			/// <remarks>This constructor is called during deserialization to reconstitute the exception object transmitted over a stream.</remarks>
			protected TemplateNotFoundException(System.Runtime.Serialization.SerializationInfo info, System.Runtime.Serialization.StreamingContext context) : base(info, context)
			{
			}

			private static string BuildMessageString(string templateName, Skyline.DataMiner.Library.Common.IDmsProtocol protocol)
			{
				if (protocol == null)
				{
					throw new System.ArgumentNullException("protocol");
				}

				return System.String.Format(System.Globalization.CultureInfo.InvariantCulture, "Template \"{0}\" for protocol \"{1}\" version \"{2}\" was not found.", templateName, protocol.Name, protocol.Version);
			}
		}

		/// <summary>
		/// Represents the parent for every type of object that can be present on a DataMiner system.
		/// </summary>
		internal abstract class DmsObject
		{
			/// <summary>
			/// The DataMiner system the object belongs to.
			/// </summary>
			protected readonly Skyline.DataMiner.Library.Common.IDms dms;
			/// <summary>
			/// Flag stating whether the DataMiner system object has been loaded.
			/// </summary>
			private bool isLoaded;
			/// <summary>
			/// Initializes a new instance of the <see cref = "DmsObject"/> class.
			/// </summary>
			/// <param name = "dms">Object implementing the <see cref = "IDms"/> interface.</param>
			protected DmsObject(Skyline.DataMiner.Library.Common.IDms dms)
			{
				if (dms == null)
				{
					throw new System.ArgumentNullException("dms");
				}

				this.dms = dms;
			}

			/// <summary>
			/// Gets the DataMiner system this object belongs to.
			/// </summary>
			public Skyline.DataMiner.Library.Common.IDms Dms
			{
				get
				{
					return dms;
				}
			}

			/// <summary>
			/// Gets the communication object.
			/// </summary>
			internal Skyline.DataMiner.Library.Common.ICommunication Communication
			{
				get
				{
					return dms.Communication;
				}
			}

			/// <summary>
			/// Gets or sets a value indicating whether or not the DMS object has been loaded.
			/// </summary>
			internal bool IsLoaded
			{
				get
				{
					return isLoaded;
				}

				set
				{
					isLoaded = value;
				}
			}

			/// <summary>
			/// Returns a value indicating whether the object exists in the DataMiner System.
			/// </summary>
			/// <returns><c>true</c> if the object exists in the DataMiner System; otherwise, <c>false</c>.</returns>
			public abstract bool Exists();
			/// <summary>
			/// Loads DMS object data in case the object exists and is not already loaded.
			/// </summary>
			internal void LoadOnDemand()
			{
				if (!IsLoaded)
				{
					Load();
				}
			}

			/// <summary>
			/// Loads the object.
			/// </summary>
			internal abstract void Load();
		}

		/// <summary>
		/// DataMiner object interface.
		/// </summary>
		public interface IDmsObject
		{
			/// <summary>
			/// Returns a value indicating whether the object exists in the DataMiner System.
			/// </summary>
			/// <returns><c>true</c> if the object exists in the DataMiner System; otherwise, <c>false</c>.</returns>
			bool Exists();
		}

		/// <summary>
		/// Represents a DataMiner element.
		/// </summary>
		internal class DmsElement : Skyline.DataMiner.Library.Common.DmsObject, Skyline.DataMiner.Library.Common.IDmsElement
		{
			/// <summary>
			///     The advanced settings.
			/// </summary>
			private Skyline.DataMiner.Library.Common.AdvancedSettings advancedSettings;
			/// <summary>
			///     The device settings.
			/// </summary>
			private Skyline.DataMiner.Library.Common.DeviceSettings deviceSettings;
			/// <summary>
			///     The DVE settings.
			/// </summary>
			private Skyline.DataMiner.Library.Common.DveSettings dveSettings;
			/// <summary>
			///     Collection of connections available on the element.
			/// </summary>
			private Skyline.DataMiner.Library.Common.IElementConnectionCollection elementCommunicationConnections;
			// Keep this message in case we need to parse the element properties when the user wants to use these.
			private Skyline.DataMiner.Net.Messages.ElementInfoEventMessage elementInfo;
			/// <summary>
			///     The failover settings.
			/// </summary>
			private Skyline.DataMiner.Library.Common.FailoverSettings failoverSettings;
			/// <summary>
			///     The general settings.
			/// </summary>
			private Skyline.DataMiner.Library.Common.GeneralSettings generalSettings;
			/// <summary>
			///     The redundancy settings.
			/// </summary>
			private Skyline.DataMiner.Library.Common.RedundancySettings redundancySettings;
			/// <summary>
			///     The replication settings.
			/// </summary>
			private Skyline.DataMiner.Library.Common.ReplicationSettings replicationSettings;
			/// <summary>
			///     The element components.
			/// </summary>
			private System.Collections.Generic.IList<Skyline.DataMiner.Library.Common.ElementSettings> settings;
			/// <summary>
			///     Initializes a new instance of the <see cref = "DmsElement"/> class.
			/// </summary>
			/// <param name = "dms">Object implementing <see cref = "IDms"/> interface.</param>
			/// <param name = "dmsElementId">The system-wide element ID.</param>
			/// <exception cref = "ArgumentNullException"><paramref name = "dms"/> is <see langword = "null"/>.</exception>
			internal DmsElement(Skyline.DataMiner.Library.Common.IDms dms, Skyline.DataMiner.Library.Common.DmsElementId dmsElementId) : base(dms)
			{
				this.Initialize();
				this.generalSettings.DmsElementId = dmsElementId;
			}

			/// <summary>
			///     Initializes a new instance of the <see cref = "DmsElement"/> class.
			/// </summary>
			/// <param name = "dms">Object implementing the <see cref = "IDms"/> interface.</param>
			/// <param name = "elementInfo">The element information.</param>
			/// <exception cref = "ArgumentNullException"><paramref name = "dms"/> is <see langword = "null"/>.</exception>
			/// <exception cref = "ArgumentNullException"><paramref name = "elementInfo"/> is <see langword = "null"/>.</exception>
			internal DmsElement(Skyline.DataMiner.Library.Common.IDms dms, Skyline.DataMiner.Net.Messages.ElementInfoEventMessage elementInfo) : base(dms)
			{
				if (elementInfo == null)
				{
					throw new System.ArgumentNullException("elementInfo");
				}

				this.Initialize(elementInfo);
				this.Parse(elementInfo);
			}

			/// <summary>
			///     Gets or sets the element description.
			/// </summary>
			/// <value>The element description.</value>
			public string Description
			{
				get
				{
					return this.GeneralSettings.Description;
				}

				set
				{
					this.GeneralSettings.Description = value;
				}
			}

			/// <summary>
			///     Gets the system-wide element ID of the element.
			/// </summary>
			/// <value>The system-wide element ID of the element.</value>
			public Skyline.DataMiner.Library.Common.DmsElementId DmsElementId
			{
				get
				{
					return this.generalSettings.DmsElementId;
				}
			}

			/// <summary>
			///     Gets the DVE settings of this element.
			/// </summary>
			/// <value>The DVE settings of this element.</value>
			public Skyline.DataMiner.Library.Common.IDveSettings DveSettings
			{
				get
				{
					return this.dveSettings;
				}
			}

			/// <summary>
			///     Gets the DataMiner Agent that hosts this element.
			/// </summary>
			/// <value>The DataMiner Agent that hosts this element.</value>
			public Skyline.DataMiner.Library.Common.IDma Host
			{
				get
				{
					return this.generalSettings.Host;
				}
			}

			/// <summary>
			///     Gets or sets the element name.
			/// </summary>
			/// <value>The element name.</value>
			/// <exception cref = "ArgumentNullException">The value of a set operation is <see langword = "null"/>.</exception>
			/// <exception cref = "ArgumentException">The value of a set operation is empty or white space.</exception>
			/// <exception cref = "ArgumentException">The value of a set operation exceeds 200 characters.</exception>
			/// <exception cref = "ArgumentException">The value of a set operation contains a forbidden character.</exception>
			/// <exception cref = "ArgumentException">The value of a set operation contains more than one '%' character.</exception>
			/// <exception cref = "NotSupportedException">A set operation is not supported on a DVE child or a derived element.</exception>
			/// <remarks>
			///     <para>The following restrictions apply to element names:</para>
			///     <list type = "bullet">
			///         <item>
			///             <para>Names may not start or end with the following characters: '.' (dot), ' ' (space).</para>
			///         </item>
			///         <item>
			///             <para>
			///                 Names may not contain the following characters: '\', '/', ':', '*', '?', '"', '&lt;', '&gt;', '|',
			///                 '�', ';'.
			///             </para>
			///         </item>
			///         <item>
			///             <para>The following characters may not occur more than once within a name: '%' (percentage).</para>
			///         </item>
			///     </list>
			/// </remarks>
			public string Name
			{
				get
				{
					return this.generalSettings.Name;
				}

				set
				{
					this.generalSettings.Name = Skyline.DataMiner.Library.Common.InputValidator.ValidateName(value, "value");
				}
			}

			/// <summary>
			///     Gets the protocol executed by this element.
			/// </summary>
			/// <value>The protocol executed by this element.</value>
			public Skyline.DataMiner.Library.Common.IDmsProtocol Protocol
			{
				get
				{
					return this.generalSettings.Protocol;
				}
			}

			/// <summary>
			///     Gets the redundancy settings.
			/// </summary>
			/// <value>The redundancy settings.</value>
			public Skyline.DataMiner.Library.Common.IRedundancySettings RedundancySettings
			{
				get
				{
					return this.redundancySettings;
				}
			}

			/// <summary>
			///     Gets the element state.
			/// </summary>
			/// <value>The element state.</value>
			public Skyline.DataMiner.Library.Common.ElementState State
			{
				get
				{
					return this.GeneralSettings.State;
				}

				internal set
				{
					this.GeneralSettings.State = value;
				}
			}

			/// <summary>
			///     Gets the general settings of the element.
			/// </summary>
			internal Skyline.DataMiner.Library.Common.GeneralSettings GeneralSettings
			{
				get
				{
					return this.generalSettings;
				}
			}

			/// <summary>
			///     Determines whether this DataMiner element exists in the DataMiner System.
			/// </summary>
			/// <returns><c>true</c> if the DataMiner element exists in the DataMiner System; otherwise, <c>false</c>.</returns>
			public override bool Exists()
			{
				return this.Dms.ElementExists(this.DmsElementId);
			}

			/// <summary>
			///     Pauses the element.
			/// </summary>
			/// <exception cref = "NotSupportedException">The element is a DVE child or derived element.</exception>
			/// <exception cref = "ElementNotFoundException">The element was not found in the DataMiner System.</exception>
			public void Pause()
			{
				if (this.DveSettings.IsChild)
				{
					throw new System.NotSupportedException("Pausing a DVE child is not supported.");
				}

				if (this.RedundancySettings.IsDerived)
				{
					throw new System.NotSupportedException("Pausing a derived element is not supported.");
				}

				this.ChangeElementState(Skyline.DataMiner.Library.Common.ElementState.Paused);
			}

			/// <summary>
			///     Starts the element.
			/// </summary>
			/// <exception cref = "NotSupportedException">The element is a DVE child or derived element.</exception>
			/// <exception cref = "ElementNotFoundException">The element was not found in the DataMiner System.</exception>
			public void Start()
			{
				if (this.DveSettings.IsChild)
				{
					throw new System.NotSupportedException("Starting a DVE child is not supported.");
				}

				if (this.RedundancySettings.IsDerived)
				{
					throw new System.NotSupportedException("Starting a derived element is not supported.");
				}

				this.ChangeElementState(Skyline.DataMiner.Library.Common.ElementState.Active);
			}

			/// <summary>
			///     Stops the element.
			/// </summary>
			/// <exception cref = "NotSupportedException">The element is a DVE child or derived element.</exception>
			/// <exception cref = "ElementNotFoundException">The element was not found in the DataMiner System.</exception>
			public void Stop()
			{
				if (this.DveSettings.IsChild)
				{
					throw new System.NotSupportedException("Stopping a DVE child is not supported.");
				}

				if (this.RedundancySettings.IsDerived)
				{
					throw new System.NotSupportedException("Stopping a derived element is not supported.");
				}

				this.ChangeElementState(Skyline.DataMiner.Library.Common.ElementState.Stopped);
			}

			/// <summary>
			///     Returns a string that represents the current object.
			/// </summary>
			/// <returns>A string that represents the current object.</returns>
			public override string ToString()
			{
				var sb = new System.Text.StringBuilder();
				sb.AppendFormat(System.Globalization.CultureInfo.InvariantCulture, "Name: {0}{1}", this.Name, System.Environment.NewLine);
				sb.AppendFormat(System.Globalization.CultureInfo.InvariantCulture, "agent ID/element ID: {0}{1}", this.DmsElementId.Value, System.Environment.NewLine);
				sb.AppendFormat(System.Globalization.CultureInfo.InvariantCulture, "Description: {0}{1}", this.Description, System.Environment.NewLine);
				sb.AppendFormat(System.Globalization.CultureInfo.InvariantCulture, "Protocol name: {0}{1}", this.Protocol.Name, System.Environment.NewLine);
				sb.AppendFormat(System.Globalization.CultureInfo.InvariantCulture, "Protocol version: {0}{1}", this.Protocol.Version, System.Environment.NewLine);
				sb.AppendFormat(System.Globalization.CultureInfo.InvariantCulture, "Hosting agent ID: {0}{1}", this.Host.Id, System.Environment.NewLine);
				return sb.ToString();
			}

			/// <summary>
			///     Loads all the data and properties found related to the element.
			/// </summary>
			/// <exception cref = "ElementNotFoundException">The element was not found in the DataMiner system.</exception>
			internal override void Load()
			{
				try
				{
					this.IsLoaded = true;
					var message = new Skyline.DataMiner.Net.Messages.GetElementByIDMessage(this.generalSettings.DmsElementId.AgentId, this.generalSettings.DmsElementId.ElementId);
					var response = (Skyline.DataMiner.Net.Messages.ElementInfoEventMessage)this.Communication.SendSingleResponseMessage(message);
					this.elementCommunicationConnections = new Skyline.DataMiner.Library.Common.ElementConnectionCollection(response);
					this.Parse(response);
				}
				catch (Skyline.DataMiner.Net.Exceptions.DataMinerException e)
				{
					if (e.ErrorCode == -2146233088)
					{
						// 0x80131500, Element "[element ID]" is unavailable.
						throw new Skyline.DataMiner.Library.Common.ElementNotFoundException(this.DmsElementId, e);
					}

					throw;
				}
			}

			/// <summary>
			///     Parses all of the element info.
			/// </summary>
			/// <param name = "elementInfo">The element info message.</param>
			internal void Parse(Skyline.DataMiner.Net.Messages.ElementInfoEventMessage elementInfo)
			{
				this.IsLoaded = true;
				try
				{
					this.ParseElementInfo(elementInfo);
				}
				catch
				{
					this.IsLoaded = false;
					throw;
				}
			}

			/// <summary>
			///     Changes the state of an element.
			/// </summary>
			/// <param name = "newState">Specifies the state that should be assigned to the element.</param>
			/// <exception cref = "ElementNotFoundException">The element was not found in the DataMiner system.</exception>
			private void ChangeElementState(Skyline.DataMiner.Library.Common.ElementState newState)
			{
				if (this.generalSettings.State == Skyline.DataMiner.Library.Common.ElementState.Deleted)
				{
					throw new Skyline.DataMiner.Library.Common.ElementNotFoundException(this.DmsElementId);
				}

				try
				{
					var message = new Skyline.DataMiner.Net.Messages.SetElementStateMessage { BState = false, DataMinerID = this.generalSettings.DmsElementId.AgentId, ElementId = this.generalSettings.DmsElementId.ElementId, State = (Skyline.DataMiner.Net.Messages.ElementState)newState };
					this.Communication.SendMessage(message);
					// Set the value in the element.
					this.generalSettings.State = newState == Skyline.DataMiner.Library.Common.ElementState.Restart ? Skyline.DataMiner.Library.Common.ElementState.Active : newState;
				}
				catch (Skyline.DataMiner.Net.Exceptions.DataMinerException e)
				{
					if (!this.Exists())
					{
						this.generalSettings.State = Skyline.DataMiner.Library.Common.ElementState.Deleted;
						throw new Skyline.DataMiner.Library.Common.ElementNotFoundException(this.DmsElementId, e);
					}

					throw;
				}
			}

			/// <summary>
			///     Initializes the element.
			/// </summary>
			private void Initialize(Skyline.DataMiner.Net.Messages.ElementInfoEventMessage elementInfo)
			{
				this.elementInfo = elementInfo;
				this.generalSettings = new Skyline.DataMiner.Library.Common.GeneralSettings(this);
				this.deviceSettings = new Skyline.DataMiner.Library.Common.DeviceSettings(this);
				this.replicationSettings = new Skyline.DataMiner.Library.Common.ReplicationSettings(this);
				this.advancedSettings = new Skyline.DataMiner.Library.Common.AdvancedSettings(this);
				this.failoverSettings = new Skyline.DataMiner.Library.Common.FailoverSettings(this);
				this.redundancySettings = new Skyline.DataMiner.Library.Common.RedundancySettings(this);
				this.dveSettings = new Skyline.DataMiner.Library.Common.DveSettings(this);
				this.elementCommunicationConnections = new Skyline.DataMiner.Library.Common.ElementConnectionCollection(this.elementInfo);
				this.settings = new System.Collections.Generic.List<Skyline.DataMiner.Library.Common.ElementSettings> { this.generalSettings, this.deviceSettings, this.replicationSettings, this.advancedSettings, this.failoverSettings, this.redundancySettings, this.dveSettings };
			}

			/// <summary>
			///     Initializes the element.
			/// </summary>
			private void Initialize()
			{
				this.generalSettings = new Skyline.DataMiner.Library.Common.GeneralSettings(this);
				this.deviceSettings = new Skyline.DataMiner.Library.Common.DeviceSettings(this);
				this.replicationSettings = new Skyline.DataMiner.Library.Common.ReplicationSettings(this);
				this.advancedSettings = new Skyline.DataMiner.Library.Common.AdvancedSettings(this);
				this.failoverSettings = new Skyline.DataMiner.Library.Common.FailoverSettings(this);
				this.redundancySettings = new Skyline.DataMiner.Library.Common.RedundancySettings(this);
				this.dveSettings = new Skyline.DataMiner.Library.Common.DveSettings(this);
				this.settings = new System.Collections.Generic.List<Skyline.DataMiner.Library.Common.ElementSettings> { this.generalSettings, this.deviceSettings, this.replicationSettings, this.advancedSettings, this.failoverSettings, this.redundancySettings, this.dveSettings };
			}

			/// <summary>
			///     Parse an ElementPortInfo object in order to add IElementConnection objects to the ElementConnectionCollection.
			/// </summary>
			/// <param name = "info">The ElementPortInfo object.</param>
			private void ParseConnection(Skyline.DataMiner.Net.Messages.ElementPortInfo info)
			{
				switch (info.ProtocolType)
				{
					case Skyline.DataMiner.Net.Messages.ProtocolType.Virtual:
						var myVirtualConnection = new Skyline.DataMiner.Library.Common.VirtualConnection(info);
						this.elementCommunicationConnections[info.PortID] = myVirtualConnection;
						break;
					case Skyline.DataMiner.Net.Messages.ProtocolType.SnmpV1:
						var mySnmpV1Connection = new Skyline.DataMiner.Library.Common.SnmpV1Connection(info);
						this.elementCommunicationConnections[info.PortID] = mySnmpV1Connection;
						break;
					case Skyline.DataMiner.Net.Messages.ProtocolType.SnmpV2:
						var mySnmpv2Connection = new Skyline.DataMiner.Library.Common.SnmpV2Connection(info);
						this.elementCommunicationConnections[info.PortID] = mySnmpv2Connection;
						break;
					case Skyline.DataMiner.Net.Messages.ProtocolType.SnmpV3:
						var mySnmpV3Connection = new Skyline.DataMiner.Library.Common.SnmpV3Connection(info);
						this.elementCommunicationConnections[info.PortID] = mySnmpV3Connection;
						break;
					case Skyline.DataMiner.Net.Messages.ProtocolType.Http:
						var myHttpConnection = new Skyline.DataMiner.Library.Common.HttpConnection(info);
						this.elementCommunicationConnections[info.PortID] = myHttpConnection;
						break;
					default:
						var myConnection = new Skyline.DataMiner.Library.Common.RealConnection(info);
						this.elementCommunicationConnections[info.PortID] = myConnection;
						break;
				}
			}

			/// <summary>
			///     Parse an ElementInfoEventMessage object.
			/// </summary>
			/// <param name = "elementInfo"></param>
			private void ParseConnections(Skyline.DataMiner.Net.Messages.ElementInfoEventMessage elementInfo)
			{
				// Keep this object in case properties are accessed.
				this.elementInfo = elementInfo;
				this.ParseConnection(elementInfo.MainPort);
				if (elementInfo.ExtraPorts != null)
				{
					foreach (Skyline.DataMiner.Net.Messages.ElementPortInfo info in elementInfo.ExtraPorts)
					{
						this.ParseConnection(info);
					}
				}
			}

			/// <summary>
			///     Parses the element info.
			/// </summary>
			/// <param name = "elementInfo">The element info.</param>
			private void ParseElementInfo(Skyline.DataMiner.Net.Messages.ElementInfoEventMessage elementInfo)
			{
				// Keep this object in case properties are accessed.
				this.elementInfo = elementInfo;
				foreach (Skyline.DataMiner.Library.Common.ElementSettings component in this.settings)
				{
					component.Load(elementInfo);
				}

				this.ParseConnections(elementInfo);
			}
		}

		/// <summary>
		/// DataMiner element interface.
		/// </summary>
		public interface IDmsElement : Skyline.DataMiner.Library.Common.IDmsObject, Skyline.DataMiner.Library.Common.IUpdateable
		{
			/// <summary>
			/// Gets or sets the element description.
			/// </summary>
			/// <value>The element description.</value>
			string Description
			{
				get;
				set;
			}

			/// <summary>
			/// Gets the system-wide element ID of the element.
			/// </summary>
			/// <value>The system-wide element ID of the element.</value>
			Skyline.DataMiner.Library.Common.DmsElementId DmsElementId
			{
				get;
			}

			/// <summary>
			/// Gets the DVE settings of this element.
			/// </summary>
			/// <value>The DVE settings of this element.</value>
			Skyline.DataMiner.Library.Common.IDveSettings DveSettings
			{
				get;
			}

			/// <summary>
			/// Gets the DataMiner Agent that hosts this element.
			/// </summary>
			/// <value>The DataMiner Agent that hosts this element.</value>
			Skyline.DataMiner.Library.Common.IDma Host
			{
				get;
			}

			/// <summary>
			/// Gets or sets the element name.
			/// </summary>
			/// <value>The element name.</value>
			/// <exception cref = "ArgumentNullException">The value of a set operation is <see langword = "null"/>.</exception>
			/// <exception cref = "ArgumentException">The value of a set operation is empty or white space.</exception>
			/// <exception cref = "ArgumentException">The value of a set operation exceeds 200 characters.</exception>
			/// <exception cref = "ArgumentException">The value of a set operation contains a forbidden character.</exception>
			/// <exception cref = "ArgumentException">The value of a set operation contains more than one '%' character.</exception>
			/// <exception cref = "NotSupportedException">A set operation is not supported on a DVE child or a derived element.</exception>
			/// <remarks>
			/// <para>The following restrictions apply to element names:</para>
			/// <list type = "bullet">
			///		<item><para>Names may not start or end with the following characters: '.' (dot), ' ' (space).</para></item>
			///		<item><para>Names may not contain the following characters: '\', '/', ':', '*', '?', '"', '&lt;', '&gt;', '|', '�', ';'.</para></item>
			///		<item><para>The following characters may not occur more than once within a name: '%' (percentage).</para></item>
			/// </list>
			/// </remarks>
			string Name
			{
				get;
				set;
			}

			/// <summary>
			/// Gets the protocol executed by this element.
			/// </summary>
			/// <value>The protocol executed by this element.</value>
			Skyline.DataMiner.Library.Common.IDmsProtocol Protocol
			{
				get;
			}

			/// <summary>
			/// Gets the redundancy settings.
			/// </summary>
			/// <value>The redundancy settings.</value>
			Skyline.DataMiner.Library.Common.IRedundancySettings RedundancySettings
			{
				get;
			}

			/// <summary>
			/// Gets the element state.
			/// </summary>
			/// <value>The element state.</value>
			Skyline.DataMiner.Library.Common.ElementState State
			{
				get;
			}

			/// <summary>
			/// Pauses the element.
			/// </summary>
			/// <exception cref = "NotSupportedException">The element is a DVE child or derived element.</exception>
			/// <exception cref = "ElementNotFoundException">The element was not found in the DataMiner System.</exception>
			void Pause();
			/// <summary>
			/// Starts the element.
			/// </summary>
			/// <exception cref = "NotSupportedException">The element is a DVE child or derived element.</exception>
			/// <exception cref = "ElementNotFoundException">The element was not found in the DataMiner System.</exception>
			void Start();
			/// <summary>
			/// Stops the element.
			/// </summary>
			/// <exception cref = "NotSupportedException">The element is a DVE child or derived element.</exception>
			/// <exception cref = "ElementNotFoundException">The element was not found in the DataMiner System.</exception>
			void Stop();
		}

		/// <summary>
		/// Base class for all connection related objects.
		/// </summary>
		public abstract class ConnectionSettings
		{
			/// <summary>
			/// Enum used to track changes on properties of classes implementing this abstract class
			/// </summary>
			protected enum ConnectionSetting
			{
				/// <summary>
				/// GetCommunityString
				/// </summary>
				GetCommunityString = 0,
				/// <summary>
				/// SetCommunityString
				/// </summary>
				SetCommunityString = 1,
				/// <summary>
				/// DeviceAddress
				/// </summary>
				DeviceAddress = 2,
				/// <summary>
				/// Timeout
				/// </summary>
				Timeout = 3,
				/// <summary>
				/// Retries
				/// </summary>
				Retries = 4,
				/// <summary>
				/// ElementTimeout
				/// </summary>
				ElementTimeout = 5,
				/// <summary>
				/// PortConnection (e.g.Udp , Tcp)
				/// </summary>
				PortConnection = 6,
				/// <summary>
				/// SecurityConfiguration
				/// </summary>
				SecurityConfig = 7,
				/// <summary>
				/// SNMPv3 Encryption Algorithm
				/// </summary>
				EncryptionAlgorithm = 8,
				/// <summary>
				/// SNMPv3 AuthenticationProtocol
				/// </summary>
				AuthenticationProtocol = 9,
				/// <summary>
				/// SNMPv3 EncryptionKey
				/// </summary>
				EncryptionKey = 10,
				/// <summary>
				/// SNMPv3 AuthenticationKey
				/// </summary>
				AuthenticationKey = 11,
				/// <summary>
				/// SNMPv3 Username
				/// </summary>
				Username = 12,
				/// <summary>
				/// SNMPv3 Security Level and Protocol
				/// </summary>
				SecurityLevelAndProtocol = 13,
				/// <summary>
				/// Local port
				/// </summary>
				LocalPort = 14,
				/// <summary>
				/// Remote port
				/// </summary>
				RemotePort = 15,
				/// <summary>
				/// Is SSL/TLS enabled
				/// </summary>
				IsSslTlsEnabled = 16,
				/// <summary>
				/// Remote host
				/// </summary>
				RemoteHost = 17,
				/// <summary>
				/// Network interface card
				/// </summary>
				NetworkInterfaceCard = 18,
				/// <summary>
				/// Bus address
				/// </summary>
				BusAddress = 19,
				/// <summary>
				/// Is BypassProxy enabled.
				/// </summary>
				IsByPassProxyEnabled
			}

			/// <summary>
			/// The list of changed properties.
			/// </summary>
			private readonly System.Collections.Generic.List<Skyline.DataMiner.Library.Common.ConnectionSettings.ConnectionSetting> changedPropertyList = new System.Collections.Generic.List<Skyline.DataMiner.Library.Common.ConnectionSettings.ConnectionSetting>();
			/// <summary>
			/// Gets the list of updated properties.
			/// </summary>
			protected System.Collections.Generic.List<Skyline.DataMiner.Library.Common.ConnectionSettings.ConnectionSetting> ChangedPropertyList
			{
				get
				{
					return changedPropertyList;
				}
			}
		}

		/// <summary>
		/// Class representing an HTTP Connection.
		/// </summary>
		public class HttpConnection : Skyline.DataMiner.Library.Common.ConnectionSettings, Skyline.DataMiner.Library.Common.IHttpConnection
		{
			private string busAddress;
			private readonly int id;
			private System.TimeSpan? elementTimeout;
			private bool isBypassProxyEnabled;
			private int retries;
			private Skyline.DataMiner.Library.Common.ITcp tcpConfiguration;
			private System.TimeSpan timeout;
			private const string BypassProxyValue = "bypassProxy";
			/// <summary>
			/// Initializes a new instance of the <see cref = "HttpConnection"/> class with default settings for Timeout (1500), Retries (3), Element Timeout (30),
			/// </summary>
			/// <param name = "tcpConfiguration">The TCP Connection.</param>
			/// <param name = "isByPassProxyEnabled">Allows you to enable the ByPassProxy setting. Default true.</param>
			/// <remarks>In case HTTPS needs to be used. TCP port needs to be 443 or the PollingIP needs to start with https:// . e.g. https://192.168.0.1</remarks>
			public HttpConnection(Skyline.DataMiner.Library.Common.ITcp tcpConfiguration, bool isByPassProxyEnabled = true)
			{
				if (tcpConfiguration == null)
					throw new System.ArgumentNullException("tcpConfiguration");
				this.tcpConfiguration = tcpConfiguration;
				this.busAddress = isByPassProxyEnabled ? BypassProxyValue : System.String.Empty;
				this.IsBypassProxyEnabled = isByPassProxyEnabled;
				this.id = -1;
				this.timeout = new System.TimeSpan(0, 0, 0, 0, 1500);
				this.retries = 3;
				this.elementTimeout = new System.TimeSpan(0, 0, 0, 30);
			}

			/// <summary>
			/// Initializes a new instance of the <see cref = "HttpConnection"/> class using the specified <see cref = "ElementPortInfo"/>.
			/// </summary>
			/// <param name = "info">Instance of <see cref = "ElementPortInfo"/> to parse the contents of.</param>
			internal HttpConnection(Skyline.DataMiner.Net.Messages.ElementPortInfo info)
			{
				this.busAddress = info.BusAddress;
				this.isBypassProxyEnabled = info.ByPassProxy;
				this.retries = info.Retries;
				this.timeout = new System.TimeSpan(0, 0, 0, 0, info.TimeoutTime);
				this.id = info.PortID;
				this.elementTimeout = new System.TimeSpan(0, 0, 0, 0, info.ElementTimeoutTime);
				this.tcpConfiguration = new Skyline.DataMiner.Library.Common.Tcp(info);
			}

			/// <summary>
			/// Initializes a new instance of the <see cref = "HttpConnection"/> class.
			/// </summary>
			public HttpConnection()
			{
			}

			/// <summary>
			/// Gets a value indicating whether to bypass the proxy.
			/// </summary>
			/// <value><c>true</c> if the proxy needs to be bypassed; otherwise, <c>false</c>.</value>
			public bool IsBypassProxyEnabled
			{
				get
				{
					return this.isBypassProxyEnabled;
				}

				set
				{
					if (this.isBypassProxyEnabled != value)
					{
						this.ChangedPropertyList.Add(Skyline.DataMiner.Library.Common.ConnectionSettings.ConnectionSetting.IsByPassProxyEnabled);
						this.isBypassProxyEnabled = value;
						this.busAddress = this.isBypassProxyEnabled ? BypassProxyValue : System.String.Empty;
					}
				}
			}
		}

		/// <summary>
		/// Represents a connection of a DataMiner element.
		/// </summary>
		public interface IElementConnection
		{
		}

		/// <summary>
		/// Represents an HTTP Connection
		/// </summary>
		public interface IHttpConnection : Skyline.DataMiner.Library.Common.IRealConnection
		{
			/// <summary>
			/// Gets a value indicating whether to bypass the proxy.
			/// </summary>
			/// <value><c>true</c> if the proxy needs to be bypassed; otherwise, <c>false</c>.</value>
			bool IsBypassProxyEnabled
			{
				get;
				set;
			}
		}

		/// <summary>
		/// Defines a non-virtual interface.
		/// </summary>
		public interface IRealConnection : Skyline.DataMiner.Library.Common.IElementConnection
		{
		}

		/// <summary>
		/// Defines an SNMP connection.
		/// </summary>
		public interface ISnmpConnection : Skyline.DataMiner.Library.Common.IRealConnection
		{
		}

		/// <summary>
		/// Defines an SNMPv1 Connection
		/// </summary>
		public interface ISnmpV1Connection : Skyline.DataMiner.Library.Common.ISnmpConnection
		{
		}

		/// <summary>
		/// Defines an SNMPv2 Connection.
		/// </summary>
		public interface ISnmpV2Connection : Skyline.DataMiner.Library.Common.ISnmpConnection
		{
		}

		/// <summary>
		/// Defines an SNMPv3 Connection.
		/// </summary>
		public interface ISnmpV3Connection : Skyline.DataMiner.Library.Common.ISnmpConnection
		{
			/// <summary>
			/// Gets or sets the SNMPv3 security configuration.
			/// </summary>
			Skyline.DataMiner.Library.Common.ISnmpV3SecurityConfig SecurityConfig
			{
				get;
				set;
			}
		}

		/// <summary>
		/// Interface for SnmpV3 Security configurations.
		/// </summary>
		public interface ISnmpV3SecurityConfig
		{
		}

		/// <summary>
		/// Defines a Virtual Connection
		/// </summary>
		public interface IVirtualConnection : Skyline.DataMiner.Library.Common.IElementConnection
		{
		}

		/// <summary>
		/// Class representing any non-virtual connection.
		/// </summary>
		public class RealConnection : Skyline.DataMiner.Library.Common.ConnectionSettings, Skyline.DataMiner.Library.Common.IRealConnection
		{
			private readonly int id;
			private System.TimeSpan timeout;
			private int retries;
			/// <summary>
			/// Initiates a new RealConnection class.
			/// </summary>
			/// <param name = "info"></param>
			internal RealConnection(Skyline.DataMiner.Net.Messages.ElementPortInfo info)
			{
				this.id = info.PortID;
				this.retries = info.Retries;
				this.timeout = new System.TimeSpan(0, 0, 0, 0, info.TimeoutTime);
			}

			/// <summary>
			/// Default empty constructor.
			/// </summary>
			public RealConnection()
			{
			}
		}

		/// <summary>
		/// Class used to Encrypt data in DataMiner.
		/// </summary>
		internal class RSA
		{
			private static System.Security.Cryptography.RSAParameters publicKey;
			/// <summary>
			/// Get or Sets the Public Key.
			/// </summary>
			internal static System.Security.Cryptography.RSAParameters PublicKey
			{
				get
				{
					return publicKey;
				}

				set
				{
					publicKey = value;
				}
			}
		}

		/// <summary>
		///     Class representing an SNMPv1 connection.
		/// </summary>
		public class SnmpV1Connection : Skyline.DataMiner.Library.Common.ConnectionSettings, Skyline.DataMiner.Library.Common.ISnmpV1Connection
		{
			private readonly int id;
			private readonly System.Guid libraryCredentials;
			private string deviceAddress;
			private System.TimeSpan? elementTimeout;
			private string getCommunityString;
			private int retries;
			private string setCommunityString;
			private System.TimeSpan timeout;
			private Skyline.DataMiner.Library.Common.IUdp udpIpConfiguration;
			/// <summary>
			///     /// Initiates a new instance with default settings for Get Community String (public), Set Community String
			///     (private), Device Address (empty),
			///     Command Timeout (1500ms), Retries (3) and Element Timeout (30s).
			/// </summary>
			/// <param name = "udpConfiguration">The UDP configuration parameters.</param>
			public SnmpV1Connection(Skyline.DataMiner.Library.Common.IUdp udpConfiguration)
			{
				if (udpConfiguration == null)
				{
					throw new System.ArgumentNullException("udpConfiguration");
				}

				this.id = -1;
				this.udpIpConfiguration = udpConfiguration;
				this.getCommunityString = "public";
				this.setCommunityString = "private";
				this.deviceAddress = System.String.Empty;
				this.timeout = new System.TimeSpan(0, 0, 0, 0, 1500);
				this.retries = 3;
				this.elementTimeout = new System.TimeSpan(0, 0, 0, 30);
			}

			/// <summary>
			///     Default empty constructor
			/// </summary>
			public SnmpV1Connection()
			{
			}

			/// <summary>
			///     Initiates an new instance.
			/// </summary>
			internal SnmpV1Connection(Skyline.DataMiner.Net.Messages.ElementPortInfo info)
			{
				this.deviceAddress = info.BusAddress;
				this.retries = info.Retries;
				this.timeout = new System.TimeSpan(0, 0, 0, 0, info.TimeoutTime);
				this.libraryCredentials = info.LibraryCredential;
				// this.elementTimeout = new TimeSpan(0, 0, info.ElementTimeoutTime / 1000);
				if (this.libraryCredentials == System.Guid.Empty)
				{
					this.getCommunityString = info.GetCommunity;
					this.setCommunityString = info.SetCommunity;
				}
				else
				{
					this.getCommunityString = System.String.Empty;
					this.setCommunityString = System.String.Empty;
				}

				this.id = info.PortID;
				this.elementTimeout = new System.TimeSpan(0, 0, 0, 0, info.ElementTimeoutTime);
				this.udpIpConfiguration = new Skyline.DataMiner.Library.Common.Udp(info);
			}
		}

		/// <summary>
		///     Class representing an SnmpV2 Connection.
		/// </summary>
		public class SnmpV2Connection : Skyline.DataMiner.Library.Common.ConnectionSettings, Skyline.DataMiner.Library.Common.ISnmpV2Connection
		{
			private readonly int id;
			private readonly System.Guid libraryCredentials;
			private string deviceAddress;
			private System.TimeSpan? elementTimeout;
			private string getCommunityString;
			private int retries;
			private string setCommunityString;
			private System.TimeSpan timeout;
			private Skyline.DataMiner.Library.Common.IUdp udpIpConfiguration;
			/// <summary>
			///     Initiates a new instance with default settings for Get Community String (public), Set Community String (private),
			///     Device Address (empty),
			///     Command Timeout (1500ms), Retries (3) and Element Timeout (30s).
			/// </summary>
			/// <param name = "udpConfiguration">The UDP Connection settings.</param>
			public SnmpV2Connection(Skyline.DataMiner.Library.Common.IUdp udpConfiguration)
			{
				if (udpConfiguration == null)
				{
					throw new System.ArgumentNullException("udpConfiguration");
				}

				this.id = -1;
				this.udpIpConfiguration = udpConfiguration;
				// this.udpIpConfiguration = udpIpIpConfiguration;
				this.deviceAddress = System.String.Empty;
				this.getCommunityString = "public";
				this.setCommunityString = "private";
				this.timeout = new System.TimeSpan(0, 0, 0, 0, 1500);
				this.retries = 3;
				this.elementTimeout = new System.TimeSpan(0, 0, 0, 30);
				this.libraryCredentials = System.Guid.Empty;
			}

			/// <summary>
			///     Default empty constructor
			/// </summary>
			public SnmpV2Connection()
			{
			}

			/// <summary>
			///     Initializes a new instance.
			/// </summary>
			internal SnmpV2Connection(Skyline.DataMiner.Net.Messages.ElementPortInfo info)
			{
				this.deviceAddress = info.BusAddress;
				this.retries = info.Retries;
				this.timeout = new System.TimeSpan(0, 0, 0, 0, info.TimeoutTime);
				this.getCommunityString = info.GetCommunity;
				this.setCommunityString = info.SetCommunity;
				this.libraryCredentials = info.LibraryCredential;
				if (info.LibraryCredential == System.Guid.Empty)
				{
					this.getCommunityString = info.GetCommunity;
					this.setCommunityString = info.SetCommunity;
				}
				else
				{
					this.getCommunityString = System.String.Empty;
					this.setCommunityString = System.String.Empty;
				}

				this.id = info.PortID;
				this.elementTimeout = new System.TimeSpan(0, 0, 0, 0, info.ElementTimeoutTime);
				this.udpIpConfiguration = new Skyline.DataMiner.Library.Common.Udp(info);
			}
		}

		/// <summary>
		///     Class representing a SNMPv3 class.
		/// </summary>
		public class SnmpV3Connection : Skyline.DataMiner.Library.Common.ConnectionSettings, Skyline.DataMiner.Library.Common.ISnmpV3Connection
		{
			private readonly int id;
			private readonly System.Guid libraryCredentials;
			private string deviceAddress;
			private System.TimeSpan? elementTimeout;
			private int retries;
			private Skyline.DataMiner.Library.Common.ISnmpV3SecurityConfig securityConfig;
			private System.TimeSpan timeout;
			private Skyline.DataMiner.Library.Common.IUdp udpIpConfiguration;
			/// <summary>
			///     Initializes a new instance.
			/// </summary>
			/// <param name = "udpConfiguration">The udp configuration settings.</param>
			/// <param name = "securityConfig">The SNMPv3 security configuration.</param>
			public SnmpV3Connection(Skyline.DataMiner.Library.Common.IUdp udpConfiguration, Skyline.DataMiner.Library.Common.SnmpV3SecurityConfig securityConfig)
			{
				if (udpConfiguration == null)
				{
					throw new System.ArgumentNullException("udpConfiguration");
				}

				if (securityConfig == null)
				{
					throw new System.ArgumentNullException("securityConfig");
				}

				this.libraryCredentials = System.Guid.Empty;
				this.id = -1;
				this.udpIpConfiguration = udpConfiguration;
				this.deviceAddress = System.String.Empty;
				this.securityConfig = securityConfig;
				this.timeout = new System.TimeSpan(0, 0, 0, 0, 1500);
				this.retries = 3;
				this.elementTimeout = new System.TimeSpan(0, 0, 0, 30);
			}

			/// <summary>
			///     Default empty constructor
			/// </summary>
			public SnmpV3Connection()
			{
			}

			/// <summary>
			///     Initializes a new instance.
			/// </summary>
			internal SnmpV3Connection(Skyline.DataMiner.Net.Messages.ElementPortInfo info)
			{
				this.deviceAddress = info.BusAddress;
				this.retries = info.Retries;
				this.timeout = new System.TimeSpan(0, 0, 0, 0, info.TimeoutTime);
				this.elementTimeout = new System.TimeSpan(0, 0, info.ElementTimeoutTime / 1000);
				if (this.libraryCredentials == System.Guid.Empty)
				{
					var securityLevelAndProtocol = Skyline.DataMiner.Library.Common.SnmpV3SecurityLevelAndProtocolAdapter.FromSLNetStopBits(info.StopBits);
					var encryptionAlgorithm = Skyline.DataMiner.Library.Common.SnmpV3EncryptionAlgorithmAdapter.FromSLNetFlowControl(info.FlowControl);
					var authenticationProtocol = Skyline.DataMiner.Library.Common.SnmpV3AuthenticationAlgorithmAdapter.FromSLNetParity(info.Parity);
					string authenticationKey = info.GetCommunity;
					string encryptionKey = info.SetCommunity;
					string username = info.DataBits;
					this.securityConfig = new Skyline.DataMiner.Library.Common.SnmpV3SecurityConfig(securityLevelAndProtocol, username, authenticationKey, encryptionKey, authenticationProtocol, encryptionAlgorithm);
				}
				else
				{
					this.SecurityConfig = new Skyline.DataMiner.Library.Common.SnmpV3SecurityConfig(Skyline.DataMiner.Library.Common.SnmpV3SecurityLevelAndProtocol.DefinedInCredentialsLibrary, System.String.Empty, System.String.Empty, System.String.Empty, Skyline.DataMiner.Library.Common.SnmpV3AuthenticationAlgorithm.DefinedInCredentialsLibrary, Skyline.DataMiner.Library.Common.SnmpV3EncryptionAlgorithm.DefinedInCredentialsLibrary);
				}

				this.id = info.PortID;
				this.elementTimeout = new System.TimeSpan(0, 0, 0, 0, info.ElementTimeoutTime);
				this.udpIpConfiguration = new Skyline.DataMiner.Library.Common.Udp(info);
			}

			/// <summary>
			///     Gets or sets the SNMPv3 security configuration.
			/// </summary>
			public Skyline.DataMiner.Library.Common.ISnmpV3SecurityConfig SecurityConfig
			{
				get
				{
					return this.securityConfig;
				}

				set
				{
					this.ChangedPropertyList.Add(Skyline.DataMiner.Library.Common.ConnectionSettings.ConnectionSetting.SecurityConfig);
					this.securityConfig = value;
				}
			}
		}

		/// <summary>
		/// Allows adapting the enum to other library equivalents.
		/// </summary>
		internal static class SnmpV3EncryptionAlgorithmAdapter
		{
			/// <summary>
			/// Converts SLNet flowControl string into the enum.
			/// </summary>
			/// <param name = "flowControl">flowControl string received from SLNet.</param>
			/// <returns>The equivalent enum.</returns>
			public static Skyline.DataMiner.Library.Common.SnmpV3EncryptionAlgorithm FromSLNetFlowControl(string flowControl)
			{
				string noCaseFlowControl = flowControl.ToUpper();
				switch (noCaseFlowControl)
				{
					case "DES":
						return Skyline.DataMiner.Library.Common.SnmpV3EncryptionAlgorithm.Des;
					case "AES128":
						return Skyline.DataMiner.Library.Common.SnmpV3EncryptionAlgorithm.Aes128;
					case "AES192":
						return Skyline.DataMiner.Library.Common.SnmpV3EncryptionAlgorithm.Aes192;
					case "AES256":
						return Skyline.DataMiner.Library.Common.SnmpV3EncryptionAlgorithm.Aes256;
					case "DEFINEDINCREDENTIALSLIBRARY":
						return Skyline.DataMiner.Library.Common.SnmpV3EncryptionAlgorithm.DefinedInCredentialsLibrary;
					case "NONE":
					default:
						return Skyline.DataMiner.Library.Common.SnmpV3EncryptionAlgorithm.None;
				}
			}
		}

		/// <summary>
		///     Represents the Security settings linked to SNMPv3.
		/// </summary>
		public class SnmpV3SecurityConfig : Skyline.DataMiner.Library.Common.ConnectionSettings, Skyline.DataMiner.Library.Common.ISnmpV3SecurityConfig
		{
			private Skyline.DataMiner.Library.Common.SnmpV3AuthenticationAlgorithm authenticationAlgorithm;
			private string authenticationKey;
			private Skyline.DataMiner.Library.Common.SnmpV3EncryptionAlgorithm encryptionAlgorithm;
			private string encryptionKey;
			private Skyline.DataMiner.Library.Common.SnmpV3SecurityLevelAndProtocol securityLevelAndProtocol;
			private string username;
			/// <summary>
			///     Initializes a new instance using No Authentication and No Privacy.
			/// </summary>
			/// <param name = "username">The username.</param>
			/// <exception cref = "System.ArgumentNullException">When the username is null.</exception>
			public SnmpV3SecurityConfig(string username)
			{
				if (username == null)
				{
					throw new System.ArgumentNullException("username");
				}

				this.securityLevelAndProtocol = Skyline.DataMiner.Library.Common.SnmpV3SecurityLevelAndProtocol.NoAuthenticationNoPrivacy;
				this.username = username;
				this.authenticationKey = string.Empty;
				this.encryptionKey = string.Empty;
				this.authenticationAlgorithm = Skyline.DataMiner.Library.Common.SnmpV3AuthenticationAlgorithm.None;
				this.encryptionAlgorithm = Skyline.DataMiner.Library.Common.SnmpV3EncryptionAlgorithm.None;
			}

			/// <summary>
			///     Initializes a new instance using Authentication No Privacy.
			/// </summary>
			/// <param name = "username">The username.</param>
			/// <param name = "authenticationKey">The Authentication key.</param>
			/// <param name = "authenticationAlgorithm">The Authentication Algorithm.</param>
			/// <exception cref = "System.ArgumentNullException">When username, authenticationKey is null.</exception>
			/// <exception cref = "IncorrectDataException">
			///     When None or DefinedInCredentialsLibrary is selected as authentication
			///     algorithm.
			/// </exception>
			public SnmpV3SecurityConfig(string username, string authenticationKey, Skyline.DataMiner.Library.Common.SnmpV3AuthenticationAlgorithm authenticationAlgorithm)
			{
				if (username == null)
				{
					throw new System.ArgumentNullException("username");
				}

				if (authenticationKey == null)
				{
					throw new System.ArgumentNullException("authenticationKey");
				}

				if (authenticationAlgorithm == Skyline.DataMiner.Library.Common.SnmpV3AuthenticationAlgorithm.None || authenticationAlgorithm == Skyline.DataMiner.Library.Common.SnmpV3AuthenticationAlgorithm.DefinedInCredentialsLibrary)
				{
					throw new Skyline.DataMiner.Library.Common.IncorrectDataException("Authentication Algorithm 'None' and 'DefinedInCredentialsLibrary' is Invalid when choosing 'Authentication No Privacy' as Security Level and Protocol.");
				}

				this.securityLevelAndProtocol = Skyline.DataMiner.Library.Common.SnmpV3SecurityLevelAndProtocol.AuthenticationNoPrivacy;
				this.username = username;
				this.authenticationKey = authenticationKey;
				this.encryptionKey = string.Empty;
				this.authenticationAlgorithm = authenticationAlgorithm;
				this.encryptionAlgorithm = Skyline.DataMiner.Library.Common.SnmpV3EncryptionAlgorithm.None;
			}

			/// <summary>
			///     Initializes a new instance using Authentication and Privacy.
			/// </summary>
			/// <param name = "username">The username.</param>
			/// <param name = "authenticationKey">The authentication key.</param>
			/// <param name = "encryptionKey">The encryptionKey.</param>
			/// <param name = "authenticationProtocol">The authentication algorithm.</param>
			/// <param name = "encryptionAlgorithm">The encryption algorithm.</param>
			/// <exception cref = "System.ArgumentNullException">When username, authenticationKey or encryptionKey is null.</exception>
			/// <exception cref = "IncorrectDataException">
			///     When None or DefinedInCredentialsLibrary is selected as authentication
			///     algorithm or encryption algorithm.
			/// </exception>
			public SnmpV3SecurityConfig(string username, string authenticationKey, Skyline.DataMiner.Library.Common.SnmpV3AuthenticationAlgorithm authenticationProtocol, string encryptionKey, Skyline.DataMiner.Library.Common.SnmpV3EncryptionAlgorithm encryptionAlgorithm)
			{
				if (username == null)
				{
					throw new System.ArgumentNullException("username");
				}

				if (authenticationKey == null)
				{
					throw new System.ArgumentNullException("authenticationKey");
				}

				if (encryptionKey == null)
				{
					throw new System.ArgumentNullException("encryptionKey");
				}

				if (authenticationProtocol == Skyline.DataMiner.Library.Common.SnmpV3AuthenticationAlgorithm.None || authenticationProtocol == Skyline.DataMiner.Library.Common.SnmpV3AuthenticationAlgorithm.DefinedInCredentialsLibrary)
				{
					throw new Skyline.DataMiner.Library.Common.IncorrectDataException("Authentication Algorithm 'None' and 'DefinedInCredentialsLibrary' is Invalid when choosing 'Authentication No Privacy' as Security Level and Protocol.");
				}

				if (encryptionAlgorithm == Skyline.DataMiner.Library.Common.SnmpV3EncryptionAlgorithm.None || encryptionAlgorithm == Skyline.DataMiner.Library.Common.SnmpV3EncryptionAlgorithm.DefinedInCredentialsLibrary)
				{
					throw new Skyline.DataMiner.Library.Common.IncorrectDataException("Encryption Algorithm 'None' and 'DefinedInCredentialsLibrary' is Invalid when choosing 'Authentication and Privacy' as Security Level and Protocol.");
				}

				this.securityLevelAndProtocol = Skyline.DataMiner.Library.Common.SnmpV3SecurityLevelAndProtocol.AuthenticationPrivacy;
				this.username = username;
				this.authenticationKey = authenticationKey;
				this.encryptionKey = encryptionKey;
				this.authenticationAlgorithm = authenticationProtocol;
				this.encryptionAlgorithm = encryptionAlgorithm;
			}

			/// <summary>
			///     Default empty constructor
			/// </summary>
			public SnmpV3SecurityConfig()
			{
			}

			/// <summary>
			///     Initializes a new instance.
			/// </summary>
			/// <param name = "securityLevelAndProtocol">The security Level and Protocol.</param>
			/// <param name = "username">The username.</param>
			/// <param name = "authenticationKey">The authenticationKey</param>
			/// <param name = "encryptionKey">The encryptionKey</param>
			/// <param name = "authenticationAlgorithm">The authentication Algorithm.</param>
			/// <param name = "encryptionAlgorithm">The encryption Algorithm.</param>
			/// <exception cref = "System.ArgumentNullException">When username, authenticationKey or encryptionKey is null.</exception>
			internal SnmpV3SecurityConfig(Skyline.DataMiner.Library.Common.SnmpV3SecurityLevelAndProtocol securityLevelAndProtocol, string username, string authenticationKey, string encryptionKey, Skyline.DataMiner.Library.Common.SnmpV3AuthenticationAlgorithm authenticationAlgorithm, Skyline.DataMiner.Library.Common.SnmpV3EncryptionAlgorithm encryptionAlgorithm)
			{
				if (username == null)
				{
					throw new System.ArgumentNullException("username");
				}

				if (authenticationKey == null)
				{
					throw new System.ArgumentNullException("authenticationKey");
				}

				if (encryptionKey == null)
				{
					throw new System.ArgumentNullException("encryptionKey");
				}

				this.securityLevelAndProtocol = securityLevelAndProtocol;
				this.username = username;
				this.authenticationKey = authenticationKey;
				this.encryptionKey = encryptionKey;
				this.authenticationAlgorithm = authenticationAlgorithm;
				this.encryptionAlgorithm = encryptionAlgorithm;
			}
		}

		/// <summary>
		/// Allows adapting the enum to other library equivalents.
		/// </summary>
		internal static class SnmpV3SecurityLevelAndProtocolAdapter
		{
			/// <summary>
			/// Converts SLNet stopBits string into the enum.
			/// </summary>
			/// <param name = "stopBits">stopBits string received from SLNet.</param>
			/// <returns>The equivalent enum.</returns>
			public static Skyline.DataMiner.Library.Common.SnmpV3SecurityLevelAndProtocol FromSLNetStopBits(string stopBits)
			{
				string noCaseStopBits = stopBits.ToUpper();
				switch (noCaseStopBits)
				{
					case "AUTHPRIV":
					case "AUTHENTICATIONPRIVACY":
						return Skyline.DataMiner.Library.Common.SnmpV3SecurityLevelAndProtocol.AuthenticationPrivacy;
					case "AUTHNOPRIV":
					case "AUTHENTICATIONNOPRIVACY":
						return Skyline.DataMiner.Library.Common.SnmpV3SecurityLevelAndProtocol.AuthenticationNoPrivacy;
					case "NOAUTHNOPRIV":
					case "NOAUTHENTICATIONNOPRIVACY":
						return Skyline.DataMiner.Library.Common.SnmpV3SecurityLevelAndProtocol.NoAuthenticationNoPrivacy;
					case "DEFINEDINCREDENTIALSLIBRARY":
						return Skyline.DataMiner.Library.Common.SnmpV3SecurityLevelAndProtocol.DefinedInCredentialsLibrary;
					default:
						return Skyline.DataMiner.Library.Common.SnmpV3SecurityLevelAndProtocol.None;
				}
			}
		}

		/// <summary>
		/// Class representing a Virtual connection. 
		/// </summary>
		public class VirtualConnection : Skyline.DataMiner.Library.Common.ConnectionSettings, Skyline.DataMiner.Library.Common.IVirtualConnection
		{
			private readonly int id;
			/// <summary>
			/// Initiates a new VirtualConnection class.
			/// </summary>
			/// <param name = "info"></param>
			internal VirtualConnection(Skyline.DataMiner.Net.Messages.ElementPortInfo info)
			{
				this.id = info.PortID;
			}

			/// <summary>
			/// Initiates a new VirtualConnection class.
			/// </summary>
			public VirtualConnection()
			{
				this.id = -1;
			}
		}

		/// <summary>
		/// Specifies the SNMPv3 authentication protocol.
		/// </summary>
		public enum SnmpV3AuthenticationAlgorithm
		{
			/// <summary>
			/// Message Digest 5 (MD5).
			/// </summary>
			Md5 = 0,
			/// <summary>
			/// Secure Hash Algorithm (SHA).
			/// </summary>
			Sha128 = 1,
			/// <summary>
			/// Secure Hash Algorithm (SHA) 224.
			/// </summary>
			Sha224 = 2,
			/// <summary>
			/// Secure Hash Algorithm (SHA) 256.
			/// </summary>
			Sha256 = 3,
			/// <summary>
			/// Secure Hash Algorithm (SHA) 384.
			/// </summary>
			Sha384 = 4,
			/// <summary>
			/// Secure Hash Algorithm (SHA) 512.
			/// </summary>
			Sha512 = 5,
			/// <summary>
			/// Algorithm is defined in the Credential Library.
			/// </summary>
			DefinedInCredentialsLibrary = 6,
			/// <summary>
			/// No algorithm selected.
			/// </summary>
			None = 7,
		}

		/// <summary>
		/// Allows adapting the enum to other library equivalents.
		/// </summary>
		public class SnmpV3AuthenticationAlgorithmAdapter
		{
			/// <summary>
			/// Converts SLNet parity string into the enum.
			/// </summary>
			/// <param name = "parity">Parity string received from SLNet.</param>
			/// <returns>The equivalent enum.</returns>
			public static Skyline.DataMiner.Library.Common.SnmpV3AuthenticationAlgorithm FromSLNetParity(string parity)
			{
				string noCaseParity = parity.ToUpper();
				switch (noCaseParity)
				{
					case "MD5":
					case "HMAC-MD5":
						return Skyline.DataMiner.Library.Common.SnmpV3AuthenticationAlgorithm.Md5;
					case "SHA":
					case "SHA1":
					case "HMAC-SHA":
						return Skyline.DataMiner.Library.Common.SnmpV3AuthenticationAlgorithm.Sha128;
					case "SHA224":
						return Skyline.DataMiner.Library.Common.SnmpV3AuthenticationAlgorithm.Sha224;
					case "SHA256":
						return Skyline.DataMiner.Library.Common.SnmpV3AuthenticationAlgorithm.Sha256;
					case "SHA384":
						return Skyline.DataMiner.Library.Common.SnmpV3AuthenticationAlgorithm.Sha384;
					case "SHA512":
						return Skyline.DataMiner.Library.Common.SnmpV3AuthenticationAlgorithm.Sha512;
					case "DEFINEDINCREDENTIALSLIBRARY":
						return Skyline.DataMiner.Library.Common.SnmpV3AuthenticationAlgorithm.DefinedInCredentialsLibrary;
					case "NONE":
					default:
						return Skyline.DataMiner.Library.Common.SnmpV3AuthenticationAlgorithm.None;
				}
			}
		}

		/// <summary>
		/// Specifies the SNMPv3 encryption algorithm.
		/// </summary>
		public enum SnmpV3EncryptionAlgorithm
		{
			/// <summary>
			/// Data Encryption Standard (DES).
			/// </summary>
			Des = 0,
			/// <summary>
			/// Advanced Encryption Standard (AES) 128 bit.
			/// </summary>
			Aes128 = 1,
			/// <summary>
			/// Advanced Encryption Standard (AES) 192 bit.
			/// </summary>
			Aes192 = 2,
			/// <summary>
			/// Advanced Encryption Standard (AES) 256 bit.
			/// </summary>
			Aes256 = 3,
			/// <summary>
			/// Advanced Encryption Standard is defined in the Credential Library.
			/// </summary>
			DefinedInCredentialsLibrary = 4,
			/// <summary>
			/// No algorithm selected.
			/// </summary>
			None = 5,
		}

		/// <summary>
		/// Specifies the SNMP v3 security level and protocol.
		/// </summary>
		public enum SnmpV3SecurityLevelAndProtocol
		{
			/// <summary>
			/// Authentication and privacy.
			/// </summary>
			AuthenticationPrivacy = 0,
			/// <summary>
			/// Authentication but no privacy.
			/// </summary>
			AuthenticationNoPrivacy = 1,
			/// <summary>
			/// No authentication and no privacy.
			/// </summary>
			NoAuthenticationNoPrivacy = 2,
			/// <summary>
			/// Security Level and Protocol is defined in the Credential library.
			/// </summary>
			DefinedInCredentialsLibrary = 3,
			/// <summary>
			/// No algorithm selected.
			/// </summary>
			None = 4
		}

		/// <summary>
		/// Represents a connection using the Internet Protocol (IP).
		/// </summary>
		public interface IIpBased : Skyline.DataMiner.Library.Common.IPortConnection
		{
		}

		/// <summary>
		/// interface IPortConnection for which all connections will inherit from.
		/// </summary>
		public interface IPortConnection
		{
		}

		/// <summary>
		/// Represents a TCP/IP connection.
		/// </summary>
		public interface ITcp : Skyline.DataMiner.Library.Common.IIpBased
		{
		}

		/// <summary>
		/// Represents a UDP/IP connection.
		/// </summary>
		public interface IUdp : Skyline.DataMiner.Library.Common.IIpBased
		{
		}

		/// <summary>
		/// Class representing a TCP connection.
		/// </summary>
		public class Tcp : Skyline.DataMiner.Library.Common.ConnectionSettings, Skyline.DataMiner.Library.Common.ITcp
		{
			private string remoteHost;
			private int networkInterfaceCard;
			private int? localPort;
			private int? remotePort;
			private readonly bool isDedicated;
			internal Tcp(Skyline.DataMiner.Net.Messages.ElementPortInfo info)
			{
				this.remoteHost = info.PollingIPAddress;
				if (!info.PollingIPPort.Equals(System.String.Empty))
					remotePort = System.Convert.ToInt32(info.PollingIPPort);
				if (!info.LocalIPPort.Equals(System.String.Empty))
					localPort = System.Convert.ToInt32(info.LocalIPPort);
				this.isDedicated = Skyline.DataMiner.Library.Common.HelperClass.IsDedicatedConnection(info);
				int networkInterfaceId = System.String.IsNullOrWhiteSpace(info.Number) ? 0 : System.Convert.ToInt32(info.Number);
				this.networkInterfaceCard = networkInterfaceId;
			}

			/// <summary>
			/// Initializes a new instance, using default values for localPort (null=Auto) and NetworkInterfaceCard (0=Auto)
			/// </summary>
			/// <param name = "remoteHost">The IP or name of the remote host.</param>
			/// <param name = "remotePort">The port number of the remote host.</param>
			public Tcp(string remoteHost, int remotePort)
			{
				this.localPort = null;
				this.remotePort = remotePort;
				this.remoteHost = remoteHost;
				this.networkInterfaceCard = 0;
				this.isDedicated = false;
			}

			/// <summary>
			/// Default empty constructor.
			/// </summary>
			public Tcp()
			{
			}
		}

		/// <summary>
		///     Class representing an UDP connection.
		/// </summary>
		public sealed class Udp : Skyline.DataMiner.Library.Common.ConnectionSettings, Skyline.DataMiner.Library.Common.IUdp
		{
			/// <summary>
			///		Compares two instances of this object by comparing the property fields.
			/// </summary>
			/// <param name = "other">The object to compare to.</param>
			/// <returns>Boolean indicating if object is equal or not.</returns>
			public bool Equals(Skyline.DataMiner.Library.Common.Udp other)
			{
				return this.isDedicated == other.isDedicated && this.isSslTlsEnabled == other.isSslTlsEnabled && this.localPort == other.localPort && this.networkInterfaceCard == other.networkInterfaceCard && string.Equals(this.remoteHost, other.remoteHost, System.StringComparison.InvariantCulture) && this.remotePort == other.remotePort;
			}

			/// <summary>Determines whether the specified object is equal to the current object.</summary>
			/// <param name = "obj">The object to compare with the current object. </param>
			/// <returns>true if the specified object  is equal to the current object; otherwise, false.</returns>
			public override bool Equals(object obj)
			{
				if (ReferenceEquals(null, obj))
					return false;
				if (ReferenceEquals(this, obj))
					return true;
				if (obj.GetType() != this.GetType())
					return false;
				return Equals((Skyline.DataMiner.Library.Common.Udp)obj);
			}

			/// <summary>Serves as the default hash function. </summary>
			/// <returns>A hash code for the current object.</returns>
			public override int GetHashCode()
			{
				unchecked
				{
					int hashCode = this.isDedicated.GetHashCode();
					hashCode = (hashCode * 397) ^ this.isSslTlsEnabled.GetHashCode();
					hashCode = (hashCode * 397) ^ this.localPort.GetHashCode();
					hashCode = (hashCode * 397) ^ this.networkInterfaceCard;
					hashCode = (hashCode * 397) ^ (this.remoteHost != null ? System.StringComparer.InvariantCulture.GetHashCode(this.remoteHost) : 0);
					hashCode = (hashCode * 397) ^ this.remotePort.GetHashCode();
					return hashCode;
				}
			}

			private readonly bool isDedicated;
			private bool isSslTlsEnabled;
			private int? localPort;
			private int networkInterfaceCard;
			private string remoteHost;
			private int? remotePort;
			/// <summary>
			///     Initializes a new instance, using default values for localPort (null=Auto) SslTlsEnabled (false), IsDedicated
			///     (false) and NetworkInterfaceCard (0=Auto)
			/// </summary>
			/// <param name = "remoteHost">The IP or name of the remote host.</param>
			/// <param name = "remotePort">The port number of the remote host.</param>
			public Udp(string remoteHost, int remotePort)
			{
				this.localPort = null;
				this.remotePort = remotePort;
				this.isSslTlsEnabled = false;
				this.isDedicated = false;
				this.remoteHost = remoteHost;
				this.networkInterfaceCard = 0;
			}

			/// <summary>
			///     Default empty constructor
			/// </summary>
			public Udp()
			{
			}

			/// <summary>
			///     Initializes a new instance using a <see cref = "ElementPortInfo"/> object.
			/// </summary>
			/// <param name = "info"></param>
			internal Udp(Skyline.DataMiner.Net.Messages.ElementPortInfo info)
			{
				this.remoteHost = info.PollingIPAddress;
				if (!info.PollingIPPort.Equals(System.String.Empty))
					remotePort = System.Convert.ToInt32(info.PollingIPPort);
				if (!info.LocalIPPort.Equals(System.String.Empty))
					localPort = System.Convert.ToInt32(info.LocalIPPort);
				this.isDedicated = Skyline.DataMiner.Library.Common.HelperClass.IsDedicatedConnection(info);
				int networkInterfaceId = string.IsNullOrWhiteSpace(info.Number) ? 0 : System.Convert.ToInt32(info.Number);
				this.networkInterfaceCard = networkInterfaceId;
			}
		}

		/// <summary>
		/// Represents the advanced element information.
		/// </summary>
		internal class AdvancedSettings : Skyline.DataMiner.Library.Common.ElementSettings, Skyline.DataMiner.Library.Common.IAdvancedSettings
		{
			/// <summary>
			/// Value indicating whether the element is hidden.
			/// </summary>
			private bool isHidden;
			/// <summary>
			/// Value indicating whether the element is read-only.
			/// </summary>
			private bool isReadOnly;
			/// <summary>
			/// Indicates whether this is a simulated element.
			/// </summary>
			private bool isSimulation;
			/// <summary>
			/// The element timeout value.
			/// </summary>
			private System.TimeSpan timeout = new System.TimeSpan(0, 0, 30);
			/// <summary>
			/// Initializes a new instance of the <see cref = "AdvancedSettings"/> class.
			/// </summary>
			/// <param name = "dmsElement">The reference to the <see cref = "DmsElement"/> instance this object is part of.</param>
			internal AdvancedSettings(Skyline.DataMiner.Library.Common.DmsElement dmsElement) : base(dmsElement)
			{
			}

			/// <summary>
			/// Gets or sets a value indicating whether the element is hidden.
			/// </summary>
			/// <value><c>true</c> if the element is hidden; otherwise, <c>false</c>.</value>
			/// <exception cref = "NotSupportedException">A set operation is not supported on a derived element.</exception>
			public bool IsHidden
			{
				get
				{
					DmsElement.LoadOnDemand();
					return isHidden;
				}

				set
				{
					DmsElement.LoadOnDemand();
					if (DmsElement.RedundancySettings.IsDerived)
					{
						throw new System.NotSupportedException("This operation is not supported on a derived element.");
					}

					if (isHidden != value)
					{
						ChangedPropertyList.Add("IsHidden");
						isHidden = value;
					}
				}
			}

			/// <summary>
			/// Gets or sets a value indicating whether the element is read-only.
			/// </summary>
			/// <value><c>true</c> if the element is read-only; otherwise, <c>false</c>.</value>
			/// <exception cref = "NotSupportedException">A set operation is not supported on a DVE or derived element.</exception>
			public bool IsReadOnly
			{
				get
				{
					DmsElement.LoadOnDemand();
					return isReadOnly;
				}

				set
				{
					if (DmsElement.DveSettings.IsChild || DmsElement.RedundancySettings.IsDerived)
					{
						throw new System.NotSupportedException("This operation is not supported on a DVE child or derived element.");
					}

					DmsElement.LoadOnDemand();
					if (isReadOnly != value)
					{
						ChangedPropertyList.Add("IsReadOnly");
						isReadOnly = value;
					}
				}
			}

			/// <summary>
			/// Gets a value indicating whether the element is running a simulation.
			/// </summary>
			/// <value><c>true</c> if the element is running a simulation; otherwise, <c>false</c>.</value>
			public bool IsSimulation
			{
				get
				{
					DmsElement.LoadOnDemand();
					return isSimulation;
				}
			}

			/// <summary>
			/// Gets or sets the element timeout value.
			/// </summary>
			/// <value>The timeout value.</value>
			/// <exception cref = "ArgumentOutOfRangeException">The value specified for a set operation is not in the range of [0,120] s.</exception>
			/// <exception cref = "NotSupportedException">A set operation is not supported on a DVE or derived element.</exception>
			/// <remarks>Fractional seconds are ignored. For example, setting the timeout to a value of 3.5s results in setting it to 3s.</remarks>
			public System.TimeSpan Timeout
			{
				get
				{
					DmsElement.LoadOnDemand();
					return timeout;
				}

				set
				{
					if (DmsElement.DveSettings.IsChild || DmsElement.RedundancySettings.IsDerived)
					{
						throw new System.NotSupportedException("Setting the timeout is not supported on a DVE child or derived element.");
					}

					DmsElement.LoadOnDemand();
					int timeoutInSeconds = (int)value.TotalSeconds;
					if (timeoutInSeconds < 0 || timeoutInSeconds > 120)
					{
						throw new System.ArgumentOutOfRangeException("value", "The timeout value must be in the range of [0,120] s.");
					}

					if ((int)timeout.TotalSeconds != (int)value.TotalSeconds)
					{
						ChangedPropertyList.Add("Timeout");
						timeout = value;
					}
				}
			}

			/// <summary>
			/// Returns the string representation of the object.
			/// </summary>
			/// <returns>String representation of the object.</returns>
			public override string ToString()
			{
				System.Text.StringBuilder sb = new System.Text.StringBuilder();
				sb.AppendLine("ADVANCED SETTINGS:");
				sb.AppendLine("==========================");
				sb.AppendFormat(System.Globalization.CultureInfo.InvariantCulture, "Timeout: {0}{1}", Timeout, System.Environment.NewLine);
				sb.AppendFormat(System.Globalization.CultureInfo.InvariantCulture, "Hidden: {0}{1}", IsHidden, System.Environment.NewLine);
				sb.AppendFormat(System.Globalization.CultureInfo.InvariantCulture, "Simulation: {0}{1}", IsSimulation, System.Environment.NewLine);
				sb.AppendFormat(System.Globalization.CultureInfo.InvariantCulture, "Read-only: {0}{1}", IsReadOnly, System.Environment.NewLine);
				return sb.ToString();
			}

			/// <summary>
			/// Loads the information to the component.
			/// </summary>
			/// <param name = "elementInfo">The element information.</param>
			internal override void Load(Skyline.DataMiner.Net.Messages.ElementInfoEventMessage elementInfo)
			{
				timeout = new System.TimeSpan(0, 0, 0, 0, elementInfo.ElementTimeoutTime);
				isHidden = elementInfo.Hidden;
				isReadOnly = elementInfo.IsReadOnly;
				isSimulation = elementInfo.IsSimulated;
			}
		}

		/// <summary>
		///  Represents a class containing the device details of an element.
		/// </summary>
		internal class DeviceSettings : Skyline.DataMiner.Library.Common.ElementSettings
		{
			/// <summary>
			/// The type of the element.
			/// </summary>
			private string type = System.String.Empty;
			/// <summary>
			/// Initializes a new instance of the <see cref = "DeviceSettings"/> class.
			/// </summary>
			/// <param name = "dmsElement">The reference to the DmsElement where this object will be used in.</param>
			internal DeviceSettings(Skyline.DataMiner.Library.Common.DmsElement dmsElement) : base(dmsElement)
			{
			}

			/// <summary>
			/// Returns the string representation of the object.
			/// </summary>
			/// <returns>String representation of the object.</returns>
			public override string ToString()
			{
				System.Text.StringBuilder sb = new System.Text.StringBuilder();
				sb.AppendLine("DEVICE SETTINGS:");
				sb.AppendLine("==========================");
				sb.AppendLine("Type: " + type);
				return sb.ToString();
			}

			/// <summary>
			/// Loads the information to the component.
			/// </summary>
			/// <param name = "elementInfo">The element information.</param>
			internal override void Load(Skyline.DataMiner.Net.Messages.ElementInfoEventMessage elementInfo)
			{
				type = elementInfo.Type ?? System.String.Empty;
			}
		}

		/// <summary>
		/// Represents DVE information of an element.
		/// </summary>
		internal class DveSettings : Skyline.DataMiner.Library.Common.ElementSettings, Skyline.DataMiner.Library.Common.IDveSettings
		{
			/// <summary>
			/// Value indicating whether DVE creation is enabled.
			/// </summary>
			private bool isDveCreationEnabled = true;
			/// <summary>
			/// Value indicating whether this element is a parent DVE.
			/// </summary>
			private bool isParent;
			/// <summary>
			/// The parent element.
			/// </summary>
			private Skyline.DataMiner.Library.Common.IDmsElement parent;
			/// <summary>
			/// Initializes a new instance of the <see cref = "DveSettings"/> class.
			/// </summary>
			/// <param name = "dmsElement">The reference to the DmsElement where this object will be used in.</param>
			internal DveSettings(Skyline.DataMiner.Library.Common.DmsElement dmsElement) : base(dmsElement)
			{
			}

			/// <summary>
			/// Gets a value indicating whether this element is a DVE child.
			/// </summary>
			/// <value><c>true</c> if this element is a DVE child element; otherwise, <c>false</c>.</value>
			public bool IsChild
			{
				get
				{
					return parent != null;
				}
			}

			/// <summary>
			/// Gets or sets a value indicating whether DVE creation is enabled for this element.
			/// </summary>
			/// <value><c>true</c> if the element DVE generation is enabled; otherwise, <c>false</c>.</value>
			/// <exception cref = "NotSupportedException">The set operation is not supported: The element is not a DVE parent element.</exception>
			public bool IsDveCreationEnabled
			{
				get
				{
					DmsElement.LoadOnDemand();
					return isDveCreationEnabled;
				}

				set
				{
					DmsElement.LoadOnDemand();
					if (!DmsElement.DveSettings.IsParent)
					{
						throw new System.NotSupportedException("This operation is only supported on DVE parent elements.");
					}

					if (isDveCreationEnabled != value)
					{
						ChangedPropertyList.Add("IsDveCreationEnabled");
						isDveCreationEnabled = value;
					}
				}
			}

			/// <summary>
			/// Gets a value indicating whether this element is a DVE parent.
			/// </summary>
			/// <value><c>true</c> if the element is a DVE parent element; otherwise, <c>false</c>.</value>
			public bool IsParent
			{
				get
				{
					DmsElement.LoadOnDemand();
					return isParent;
				}
			}

			/// <summary>
			/// Returns the string representation of the object.
			/// </summary>
			/// <returns>String representation of the object.</returns>
			public override string ToString()
			{
				System.Text.StringBuilder sb = new System.Text.StringBuilder();
				sb.AppendLine("DVE SETTINGS:");
				sb.AppendLine("==========================");
				sb.AppendFormat(System.Globalization.CultureInfo.InvariantCulture, "DVE creation enabled: {0}{1}", IsDveCreationEnabled, System.Environment.NewLine);
				sb.AppendFormat(System.Globalization.CultureInfo.InvariantCulture, "Is parent DVE: {0}{1}", IsParent, System.Environment.NewLine);
				sb.AppendFormat(System.Globalization.CultureInfo.InvariantCulture, "Is child DVE: {0}{1}", IsChild, System.Environment.NewLine);
				if (IsChild)
				{
					sb.AppendFormat(System.Globalization.CultureInfo.InvariantCulture, "Parent DataMiner agent ID/element ID: {0}{1}", parent.DmsElementId.Value, System.Environment.NewLine);
				}

				return sb.ToString();
			}

			/// <summary>
			/// Loads the information to the component.
			/// </summary>
			/// <param name = "elementInfo">The element information.</param>
			internal override void Load(Skyline.DataMiner.Net.Messages.ElementInfoEventMessage elementInfo)
			{
				if (elementInfo.IsDynamicElement && elementInfo.DveParentDmaId != 0 && elementInfo.DveParentElementId != 0)
				{
					parent = new Skyline.DataMiner.Library.Common.DmsElement(DmsElement.Dms, new Skyline.DataMiner.Library.Common.DmsElementId(elementInfo.DveParentDmaId, elementInfo.DveParentElementId));
				}

				isParent = elementInfo.IsDveMainElement;
				isDveCreationEnabled = elementInfo.CreateDVEs;
			}
		}

		/// <summary>
		/// Represents a class containing the failover settings for an element.
		/// </summary>
		internal class FailoverSettings : Skyline.DataMiner.Library.Common.ElementSettings, Skyline.DataMiner.Library.Common.IFailoverSettings
		{
			/// <summary>
			/// In failover configurations, this can be used to force an element to run only on one specific agent.
			/// </summary>
			private string forceAgent = System.String.Empty;
			/// <summary>
			/// Is true when the element is a failover element and is online on the backup agent instead of this agent; otherwise, false.
			/// </summary>
			private bool isOnlineOnBackupAgent;
			/// <summary>
			/// Is true when the element is a failover element that needs to keep running on the same DataMiner agent event after switching; otherwise, false.
			/// </summary>
			private bool keepOnline;
			/// <summary>
			/// Initializes a new instance of the <see cref = "FailoverSettings"/> class.
			/// </summary>
			/// <param name = "dmsElement">The reference to the DmsElement where this object will be used in.</param>
			internal FailoverSettings(Skyline.DataMiner.Library.Common.DmsElement dmsElement) : base(dmsElement)
			{
			}

			/// <summary>
			/// Gets or sets a value indicating whether to force agent.
			/// Local IP address of the agent which will be running the element.
			/// </summary>
			/// <value>Value indicating whether to force agent.</value>
			public string ForceAgent
			{
				get
				{
					DmsElement.LoadOnDemand();
					return forceAgent;
				}

				set
				{
					DmsElement.LoadOnDemand();
					var newValue = value == null ? System.String.Empty : value;
					if (!forceAgent.Equals(newValue, System.StringComparison.Ordinal))
					{
						ChangedPropertyList.Add("ForceAgent");
						forceAgent = newValue;
					}
				}
			}

			/// <summary>
			/// Gets a value indicating whether the element is a failover element and is online on the backup agent instead of this agent.
			/// </summary>
			/// <value><c>true</c> if the element is a failover element and is online on the backup agent instead of this agent; otherwise, <c>false</c>.</value>
			public bool IsOnlineOnBackupAgent
			{
				get
				{
					DmsElement.LoadOnDemand();
					return isOnlineOnBackupAgent;
				}
			}

			/// <summary>
			/// Gets or sets a value indicating whether the element is a failover element that needs to keep running on the same DataMiner agent event after switching.
			/// keepOnline="true" indicates that the element needs to keep running even when the agent is offline.
			/// </summary>
			/// <value><c>true</c> if the element is a failover element that needs to keep running on the same DataMiner agent event after switching; otherwise, <c>false</c>.</value>
			public bool KeepOnline
			{
				get
				{
					DmsElement.LoadOnDemand();
					return keepOnline;
				}

				set
				{
					DmsElement.LoadOnDemand();
					if (keepOnline != value)
					{
						ChangedPropertyList.Add("KeepOnline");
						keepOnline = value;
					}
				}
			}

			/// <summary>
			/// Returns the string representation of the object.
			/// </summary>
			/// <returns>String representation of the object.</returns>
			public override string ToString()
			{
				System.Text.StringBuilder sb = new System.Text.StringBuilder();
				sb.AppendLine("FAILOVER SETTINGS:");
				sb.AppendLine("==========================");
				sb.AppendFormat(System.Globalization.CultureInfo.InvariantCulture, "Keep online: {0}{1}", KeepOnline, System.Environment.NewLine);
				sb.AppendFormat(System.Globalization.CultureInfo.InvariantCulture, "Force agent: {0}{1}", ForceAgent, System.Environment.NewLine);
				sb.AppendFormat(System.Globalization.CultureInfo.InvariantCulture, "Online on backup agent: {0}{1}", IsOnlineOnBackupAgent, System.Environment.NewLine);
				return sb.ToString();
			}

			/// <summary>
			/// Loads the information to the component.
			/// </summary>
			/// <param name = "elementInfo">The element information.</param>
			internal override void Load(Skyline.DataMiner.Net.Messages.ElementInfoEventMessage elementInfo)
			{
				keepOnline = elementInfo.KeepOnline;
				forceAgent = elementInfo.ForceAgent ?? System.String.Empty;
				isOnlineOnBackupAgent = elementInfo.IsOnlineOnBackupAgent;
			}
		}

		/// <summary>
		/// Represents general element information.
		/// </summary>
		internal class GeneralSettings : Skyline.DataMiner.Library.Common.ElementSettings
		{
			/// <summary>
			/// The name of the alarm template.
			/// </summary>
			private string alarmTemplateName;
			/// <summary>
			/// The alarm template assigned to this element.
			/// </summary>
			private Skyline.DataMiner.Library.Common.Templates.IDmsAlarmTemplate alarmTemplate;
			/// <summary>
			/// Element description.
			/// </summary>
			private string description = System.String.Empty;
			/// <summary>
			/// The hosting DataMiner agent.
			/// </summary>
			private Skyline.DataMiner.Library.Common.Dma host;
			/// <summary>
			/// The element state.
			/// </summary>
			private Skyline.DataMiner.Library.Common.ElementState state = Skyline.DataMiner.Library.Common.ElementState.Active;
			/// <summary>
			/// Instance of the protocol this element executes.
			/// </summary>
			private Skyline.DataMiner.Library.Common.DmsProtocol protocol;
			/// <summary>
			/// The trend template assigned to this element.
			/// </summary>
			private Skyline.DataMiner.Library.Common.Templates.IDmsTrendTemplate trendTemplate;
			/// <summary>
			/// The name of the element.
			/// </summary>
			private string name;
			/// <summary>
			/// Initializes a new instance of the <see cref = "GeneralSettings"/> class.
			/// </summary>
			/// <param name = "dmsElement">The reference to the DmsElement where this object will be used in.</param>
			internal GeneralSettings(Skyline.DataMiner.Library.Common.DmsElement dmsElement) : base(dmsElement)
			{
			}

			/// <summary>
			/// Gets or sets the alarm template definition of the element.
			/// This can either be an alarm template or an alarm template group.
			/// </summary>
			internal Skyline.DataMiner.Library.Common.Templates.IDmsAlarmTemplate AlarmTemplate
			{
				get
				{
					DmsElement.LoadOnDemand();
					return alarmTemplate;
				}

				set
				{
					DmsElement.LoadOnDemand();
					bool updateRequired = false;
					if (alarmTemplate == null)
					{
						if (value != null)
						{
							updateRequired = true;
						}
					}
					else
					{
						if (value == null || !alarmTemplate.Equals(value))
						{
							updateRequired = true;
						}
					}

					if (updateRequired)
					{
						ChangedPropertyList.Add("AlarmTemplate");
						alarmTemplateName = value == null ? System.String.Empty : value.Name;
						alarmTemplate = value;
					}
				}
			}

			/// <summary>
			/// Gets or sets the element description.
			/// </summary>
			internal string Description
			{
				get
				{
					DmsElement.LoadOnDemand();
					return description;
				}

				set
				{
					DmsElement.LoadOnDemand();
					string newValue = value == null ? System.String.Empty : value;
					if (!description.Equals(newValue, System.StringComparison.Ordinal))
					{
						ChangedPropertyList.Add("Description");
						description = newValue;
					}
				}
			}

			/// <summary>
			/// Gets or sets the system-wide element ID.
			/// </summary>
			internal Skyline.DataMiner.Library.Common.DmsElementId DmsElementId
			{
				get;
				set;
			}

			/// <summary>
			/// Gets the DataMiner agent that hosts the element.
			/// </summary>
			internal Skyline.DataMiner.Library.Common.Dma Host
			{
				get
				{
					DmsElement.LoadOnDemand();
					return host;
				}
			}

			/// <summary>
			/// Gets or sets the state of the element.
			/// </summary>
			internal Skyline.DataMiner.Library.Common.ElementState State
			{
				get
				{
					DmsElement.LoadOnDemand();
					return state;
				}

				set
				{
					DmsElement.LoadOnDemand();
					state = value;
				}
			}

			/// <summary>
			/// Gets or sets the trend template assigned to this element.
			/// </summary>
			internal Skyline.DataMiner.Library.Common.Templates.IDmsTrendTemplate TrendTemplate
			{
				get
				{
					DmsElement.LoadOnDemand();
					return trendTemplate;
				}

				set
				{
					DmsElement.LoadOnDemand();
					bool updateRequired = false;
					if (trendTemplate == null)
					{
						if (value != null)
						{
							updateRequired = true;
						}
					}
					else
					{
						if (value == null || !trendTemplate.Equals(value))
						{
							updateRequired = true;
						}
					}

					if (updateRequired)
					{
						ChangedPropertyList.Add("TrendTemplate");
						trendTemplate = value;
					}
				}
			}

			/// <summary>
			/// Gets or sets the name of the element.
			/// </summary>
			/// <exception cref = "NotSupportedException">A set operation is not supported on a DVE child or a derived element.</exception>
			internal string Name
			{
				get
				{
					DmsElement.LoadOnDemand();
					return name;
				}

				set
				{
					DmsElement.LoadOnDemand();
					if (DmsElement.DveSettings.IsChild || DmsElement.RedundancySettings.IsDerived)
					{
						throw new System.NotSupportedException("Setting the name of a DVE child or a derived element is not supported.");
					}

					if (!name.Equals(value, System.StringComparison.Ordinal))
					{
						ChangedPropertyList.Add("Name");
						name = value.Trim();
					}
				}
			}

			/// <summary>
			/// Gets or sets the instance of the protocol.
			/// </summary>
			/// <exception cref = "ArgumentNullException">The value of a set operation is <see langword = "null"/>.</exception>
			/// <exception cref = "ArgumentException">The value of a set operation is empty.</exception>
			internal Skyline.DataMiner.Library.Common.DmsProtocol Protocol
			{
				get
				{
					DmsElement.LoadOnDemand();
					return protocol;
				}

				set
				{
					if (value == null)
					{
						throw new System.ArgumentNullException("value");
					}

					DmsElement.LoadOnDemand();
					ChangedPropertyList.Add("Protocol");
					protocol = value;
				}
			}

			/// <summary>
			/// Returns the string representation of the object.
			/// </summary>
			/// <returns>String representation of the object.</returns>
			public override string ToString()
			{
				System.Text.StringBuilder sb = new System.Text.StringBuilder();
				sb.AppendLine("GENERAL SETTINGS:");
				sb.AppendLine("==========================");
				sb.AppendFormat(System.Globalization.CultureInfo.InvariantCulture, "Name: {0}{1}", DmsElement.Name, System.Environment.NewLine);
				sb.AppendFormat(System.Globalization.CultureInfo.InvariantCulture, "Description: {0}{1}", Description, System.Environment.NewLine);
				sb.AppendFormat(System.Globalization.CultureInfo.InvariantCulture, "Protocol name: {0}{1}", Protocol.Name, System.Environment.NewLine);
				sb.AppendFormat(System.Globalization.CultureInfo.InvariantCulture, "Protocol version: {0}{1}", Protocol.Version, System.Environment.NewLine);
				sb.AppendFormat(System.Globalization.CultureInfo.InvariantCulture, "DMA ID: {0}{1}", DmsElementId.AgentId, System.Environment.NewLine);
				sb.AppendFormat(System.Globalization.CultureInfo.InvariantCulture, "Element ID: {0}{1}", DmsElementId.ElementId, System.Environment.NewLine);
				sb.AppendFormat(System.Globalization.CultureInfo.InvariantCulture, "Hosting DMA ID: {0}{1}", Host.Id, System.Environment.NewLine);
				sb.AppendFormat(System.Globalization.CultureInfo.InvariantCulture, "Alarm template: {0}{1}", AlarmTemplate, System.Environment.NewLine);
				sb.AppendFormat(System.Globalization.CultureInfo.InvariantCulture, "Trend template: {0}{1}", TrendTemplate, System.Environment.NewLine);
				sb.AppendFormat(System.Globalization.CultureInfo.InvariantCulture, "State: {0}{1}", State, System.Environment.NewLine);
				return sb.ToString();
			}

			/// <summary>
			/// Loads the information to the component.
			/// </summary>
			/// <param name = "elementInfo">The element information.</param>
			internal override void Load(Skyline.DataMiner.Net.Messages.ElementInfoEventMessage elementInfo)
			{
				DmsElementId = new Skyline.DataMiner.Library.Common.DmsElementId(elementInfo.DataMinerID, elementInfo.ElementID);
				description = elementInfo.Description ?? System.String.Empty;
				protocol = new Skyline.DataMiner.Library.Common.DmsProtocol(DmsElement.Dms, elementInfo.Protocol, elementInfo.ProtocolVersion);
				alarmTemplateName = elementInfo.ProtocolTemplate;
				trendTemplate = System.String.IsNullOrWhiteSpace(elementInfo.Trending) ? null : new Skyline.DataMiner.Library.Common.Templates.DmsTrendTemplate(DmsElement.Dms, elementInfo.Trending, protocol);
				state = (Skyline.DataMiner.Library.Common.ElementState)elementInfo.State;
				name = elementInfo.Name ?? System.String.Empty;
				host = new Skyline.DataMiner.Library.Common.Dma(DmsElement.Dms, elementInfo.HostingAgentID);
				LoadAlarmTemplateDefinition();
			}

			/// <summary>
			/// Loads the alarm template definition.
			/// This method checks whether there is a group or a template assigned to the element.
			/// </summary>
			private void LoadAlarmTemplateDefinition()
			{
				if (alarmTemplate == null && !System.String.IsNullOrWhiteSpace(alarmTemplateName))
				{
					Skyline.DataMiner.Net.Messages.GetAlarmTemplateMessage message = new Skyline.DataMiner.Net.Messages.GetAlarmTemplateMessage { AsOneObject = true, Protocol = protocol.Name, Version = protocol.Version, Template = alarmTemplateName };
					Skyline.DataMiner.Net.Messages.AlarmTemplateEventMessage response = (Skyline.DataMiner.Net.Messages.AlarmTemplateEventMessage)DmsElement.Dms.Communication.SendSingleResponseMessage(message);
					if (response != null)
					{
						switch (response.Type)
						{
							case Skyline.DataMiner.Net.Messages.AlarmTemplateType.Template:
								alarmTemplate = new Skyline.DataMiner.Library.Common.Templates.DmsStandaloneAlarmTemplate(DmsElement.Dms, response);
								break;
							case Skyline.DataMiner.Net.Messages.AlarmTemplateType.Group:
								alarmTemplate = new Skyline.DataMiner.Library.Common.Templates.DmsAlarmTemplateGroup(DmsElement.Dms, response);
								break;
							default:
								throw new System.InvalidOperationException("Unexpected value: " + response.Type);
						}
					}
				}
			}
		}

		/// <summary>
		/// DataMiner element advanced settings interface.
		/// </summary>
		public interface IAdvancedSettings
		{
			/// <summary>
			/// Gets or sets a value indicating whether the element is hidden.
			/// </summary>
			/// <value><c>true</c> if the element is hidden; otherwise, <c>false</c>.</value>
			/// <exception cref = "NotSupportedException">A set operation is not supported on a derived element.</exception>
			bool IsHidden
			{
				get;
				set;
			}

			/// <summary>
			/// Gets or sets a value indicating whether the element is read-only.
			/// </summary>
			/// <value><c>true</c> if the element is read-only; otherwise, <c>false</c>.</value>
			/// <exception cref = "NotSupportedException">A set operation is not supported on a DVE or derived element.</exception>
			bool IsReadOnly
			{
				get;
				set;
			}

			/// <summary>
			/// Gets a value indicating whether the element is running a simulation.
			/// </summary>
			/// <value><c>true</c> if the element is running a simulation; otherwise, <c>false</c>.</value>
			bool IsSimulation
			{
				get;
			}

			/// <summary>
			/// Gets or sets the element timeout value.
			/// </summary>
			/// <value>The timeout value.</value>
			/// <exception cref = "NotSupportedException">A set operation is not supported on a DVE or derived element.</exception>
			/// <exception cref = "ArgumentOutOfRangeException">The value specified for a set operation is not in the range of [0,120] s.</exception>
			/// <remarks>Fractional seconds are ignored. For example, setting the timeout to a value of 3.5s results in setting it to 3s.</remarks>
			System.TimeSpan Timeout
			{
				get;
				set;
			}
		}

		/// <summary>
		/// DataMiner element DVE settings interface.
		/// </summary>
		public interface IDveSettings
		{
			/// <summary>
			/// Gets a value indicating whether this element is a DVE child.
			/// </summary>
			/// <value><c>true</c> if this element is a DVE child element; otherwise, <c>false</c>.</value>
			bool IsChild
			{
				get;
			}

			/// <summary>
			/// Gets or sets a value indicating whether DVE creation is enabled for this element.
			/// </summary>
			/// <value><c>true</c> if the element DVE generation is enabled; otherwise, <c>false</c>.</value>
			/// <exception cref = "NotSupportedException">The element is not a DVE parent element.</exception>
			bool IsDveCreationEnabled
			{
				get;
				set;
			}

			/// <summary>
			/// Gets a value indicating whether this element is a DVE parent.
			/// </summary>
			/// <value><c>true</c> if the element is a DVE parent element; otherwise, <c>false</c>.</value>
			bool IsParent
			{
				get;
			}
		}

		/// <summary>
		/// DataMiner element failover settings interface.
		/// </summary>
		internal interface IFailoverSettings
		{
			/// <summary>
			/// Gets or sets a value indicating whether to force agent.
			/// Local IP address of the agent which will be running the element.
			/// </summary>
			/// <value>Value indicating whether to force agent.</value>
			string ForceAgent
			{
				get;
				set;
			}

			/// <summary>
			/// Gets a value indicating whether the element is a failover element and is online on the backup agent instead of this agent.
			/// </summary>
			/// <value><c>true</c> if the element is a failover element and is online on the backup agent instead of this agent; otherwise, <c>false</c>.</value>
			bool IsOnlineOnBackupAgent
			{
				get;
			}

			/// <summary>
			/// Gets or sets a value indicating whether the element is a failover element that needs to keep running on the same DataMiner agent event after switching.
			/// </summary>
			/// <value><c>true</c> if the element is a failover element that needs to keep running on the same DataMiner agent event after switching; otherwise, <c>false</c>.</value>
			bool KeepOnline
			{
				get;
				set;
			}
		}

		/// <summary>
		/// DataMiner element redundancy settings interface.
		/// </summary>
		public interface IRedundancySettings
		{
			/// <summary>
			/// Gets a value indicating whether the element is derived from another element.
			/// </summary>
			/// <value><c>true</c> if the element is derived from another element; otherwise, <c>false</c>.</value>
			bool IsDerived
			{
				get;
			}
		}

		/// <summary>
		/// DataMiner element replication settings interface.
		/// </summary>
		public interface IReplicationSettings
		{
		}

		/// <summary>
		/// Represents the redundancy settings for a element.
		/// </summary>
		internal class RedundancySettings : Skyline.DataMiner.Library.Common.ElementSettings, Skyline.DataMiner.Library.Common.IRedundancySettings
		{
			/// <summary>
			/// Value indicating whether or not this element is derived from another element.
			/// </summary>
			private bool isDerived;
			/// <summary>
			/// Initializes a new instance of the <see cref = "RedundancySettings"/> class.
			/// </summary>
			/// <param name = "dmsElement">The reference to the <see cref = "DmsElement"/> instance this object is part of.</param>
			internal RedundancySettings(Skyline.DataMiner.Library.Common.DmsElement dmsElement) : base(dmsElement)
			{
			}

			/// <summary>
			/// Gets or sets a value indicating whether the element is derived from another element.
			/// </summary>
			/// <value><c>true</c> if the element is derived from another element; otherwise, <c>false</c>.</value>
			public bool IsDerived
			{
				get
				{
					DmsElement.LoadOnDemand();
					return isDerived;
				}

				internal set
				{
					isDerived = value;
				}
			}

			/// <summary>
			/// Returns the string representation of the object.
			/// </summary>
			/// <returns>String representation of the object.</returns>
			public override string ToString()
			{
				System.Text.StringBuilder sb = new System.Text.StringBuilder();
				sb.AppendLine("REDUNDANCY SETTINGS:");
				sb.AppendLine("==========================");
				sb.AppendFormat(System.Globalization.CultureInfo.InvariantCulture, "Derived: {0}{1}", isDerived, System.Environment.NewLine);
				return sb.ToString();
			}

			/// <summary>
			/// Loads the information to the component.
			/// </summary>
			/// <param name = "elementInfo">The element information.</param>
			internal override void Load(Skyline.DataMiner.Net.Messages.ElementInfoEventMessage elementInfo)
			{
				isDerived = elementInfo.IsDerivedElement;
			}
		}

		/// <summary>
		/// Represents the replication information of an element.
		/// </summary>
		internal class ReplicationSettings : Skyline.DataMiner.Library.Common.ElementSettings, Skyline.DataMiner.Library.Common.IReplicationSettings
		{
			/// <summary>
			/// The domain the specified user belongs to.
			/// </summary>
			private string domain = System.String.Empty;
			/// <summary>
			/// External DMP engine.
			/// </summary>
			private bool connectsToExternalDmp;
			/// <summary>
			/// IP address of the source DataMiner Agent.
			/// </summary>
			private string ipAddressSourceDma = System.String.Empty;
			/// <summary>
			/// Value indicating whether this element is replicated.
			/// </summary>
			private bool isReplicated;
			/// <summary>
			/// The options string.
			/// </summary>
			private string options = System.String.Empty;
			/// <summary>
			/// The password.
			/// </summary>
			private string password = System.String.Empty;
			/// <summary>
			/// The ID of the source element.
			/// </summary>
			private Skyline.DataMiner.Library.Common.DmsElementId sourceDmsElementId = new Skyline.DataMiner.Library.Common.DmsElementId(-1, -1);
			/// <summary>
			/// The user name.
			/// </summary>
			private string userName = System.String.Empty;
			/// <summary>
			/// Initializes a new instance of the <see cref = "ReplicationSettings"/> class.
			/// </summary>
			/// <param name = "dmsElement">The reference to the DmsElement where this object will be used in.</param>
			internal ReplicationSettings(Skyline.DataMiner.Library.Common.DmsElement dmsElement) : base(dmsElement)
			{
			}

			/// <summary>
			/// Returns the string representation of the object.
			/// </summary>
			/// <returns>String representation of the object.</returns>
			public override string ToString()
			{
				System.Text.StringBuilder sb = new System.Text.StringBuilder();
				sb.AppendLine("REPLICATION SETTINGS:");
				sb.AppendLine("==========================");
				sb.AppendFormat(System.Globalization.CultureInfo.InvariantCulture, "Replicated: {0}{1}", isReplicated, System.Environment.NewLine);
				sb.AppendFormat(System.Globalization.CultureInfo.InvariantCulture, "Source DMA ID: {0}{1}", sourceDmsElementId.AgentId, System.Environment.NewLine);
				sb.AppendFormat(System.Globalization.CultureInfo.InvariantCulture, "Source element ID: {0}{1}", sourceDmsElementId.ElementId, System.Environment.NewLine);
				sb.AppendFormat(System.Globalization.CultureInfo.InvariantCulture, "IP address source DMA: {0}{1}", ipAddressSourceDma, System.Environment.NewLine);
				sb.AppendFormat(System.Globalization.CultureInfo.InvariantCulture, "Domain: {0}{1}", domain, System.Environment.NewLine);
				sb.AppendFormat(System.Globalization.CultureInfo.InvariantCulture, "User name: {0}{1}", userName, System.Environment.NewLine);
				sb.AppendFormat(System.Globalization.CultureInfo.InvariantCulture, "Password: {0}{1}", password, System.Environment.NewLine);
				//sb.AppendFormat(CultureInfo.InvariantCulture, "Options: {0}{1}", options, Environment.NewLine);
				//sb.AppendFormat(CultureInfo.InvariantCulture, "Replication DMP engine: {0}{1}", connectsToExternalDmp, Environment.NewLine);
				return sb.ToString();
			}

			/// <summary>
			/// Loads the information to the component.
			/// </summary>
			/// <param name = "elementInfo">The element information.</param>
			internal override void Load(Skyline.DataMiner.Net.Messages.ElementInfoEventMessage elementInfo)
			{
				isReplicated = elementInfo.ReplicationActive;
				if (!isReplicated)
				{
					options = System.String.Empty;
					ipAddressSourceDma = System.String.Empty;
					password = System.String.Empty;
					domain = System.String.Empty;
					sourceDmsElementId = new Skyline.DataMiner.Library.Common.DmsElementId(-1, -1);
					userName = System.String.Empty;
					connectsToExternalDmp = false;
				}

				options = elementInfo.ReplicationOptions ?? System.String.Empty;
				ipAddressSourceDma = elementInfo.ReplicationDmaIP ?? System.String.Empty;
				password = elementInfo.ReplicationPwd ?? System.String.Empty;
				domain = elementInfo.ReplicationDomain ?? System.String.Empty;
				bool isEmpty = System.String.IsNullOrWhiteSpace(elementInfo.ReplicationRemoteElement) || elementInfo.ReplicationRemoteElement.Equals("/", System.StringComparison.Ordinal);
				if (isEmpty)
				{
					sourceDmsElementId = new Skyline.DataMiner.Library.Common.DmsElementId(-1, -1);
				}
				else
				{
					try
					{
						sourceDmsElementId = new Skyline.DataMiner.Library.Common.DmsElementId(elementInfo.ReplicationRemoteElement);
					}
					catch (System.Exception ex)
					{
						string logMessage = "Failed parsing replication element info for element " + System.Convert.ToString(elementInfo.Name) + " (" + System.Convert.ToString(elementInfo.DataMinerID) + "/" + System.Convert.ToString(elementInfo.ElementID) + "). Replication remote element is: " + System.Convert.ToString(elementInfo.ReplicationRemoteElement) + System.Environment.NewLine + ex;
						Skyline.DataMiner.Library.Common.Logger.Log(logMessage);
						sourceDmsElementId = new Skyline.DataMiner.Library.Common.DmsElementId(-1, -1);
					}
				}

				userName = elementInfo.ReplicationUser ?? System.String.Empty;
				connectsToExternalDmp = elementInfo.ReplicationIsExternalDMP;
			}
		}

		/// <summary>
		/// Represents a base class for all of the components in a DmsElement object.
		/// </summary>
		internal abstract class ElementSettings
		{
			/// <summary>
			/// The list of changed properties.
			/// </summary>
			private readonly System.Collections.Generic.List<System.String> changedPropertyList = new System.Collections.Generic.List<System.String>();
			/// <summary>
			/// Instance of the DmsElement class where these classes will be used for.
			/// </summary>
			private readonly Skyline.DataMiner.Library.Common.DmsElement dmsElement;
			/// <summary>
			/// Initializes a new instance of the <see cref = "ElementSettings"/> class.
			/// </summary>
			/// <param name = "dmsElement">The reference to the <see cref = "DmsElement"/> instance this object is part of.</param>
			protected ElementSettings(Skyline.DataMiner.Library.Common.DmsElement dmsElement)
			{
				this.dmsElement = dmsElement;
			}

			/// <summary>
			/// Gets the element this object belongs to.
			/// </summary>
			internal Skyline.DataMiner.Library.Common.DmsElement DmsElement
			{
				get
				{
					return dmsElement;
				}
			}

			/// <summary>
			/// Gets the list of updated properties.
			/// </summary>
			protected internal System.Collections.Generic.List<System.String> ChangedPropertyList
			{
				get
				{
					return changedPropertyList;
				}
			}

			/// <summary>
			/// Based on the array provided from the DmsNotify call, parse the data to the correct fields.
			/// </summary>
			/// <param name = "elementInfo">Object containing all the required information. Retrieved by DmsClass.</param>
			internal abstract void Load(Skyline.DataMiner.Net.Messages.ElementInfoEventMessage elementInfo);
		}

		/// <summary>
		/// Represents a DataMiner protocol.
		/// </summary>
		internal class DmsProtocol : Skyline.DataMiner.Library.Common.DmsObject, Skyline.DataMiner.Library.Common.IDmsProtocol
		{
			/// <summary>
			/// The constant value 'Production'.
			/// </summary>
			private const string Production = "Production";
			/// <summary>
			/// The protocol name.
			/// </summary>
			private string name;
			/// <summary>
			/// The protocol version.
			/// </summary>
			private string version;
			/// <summary>
			/// The type of the protocol.
			/// </summary>
			private Skyline.DataMiner.Library.Common.ProtocolType type;
			/// <summary>
			/// The protocol referenced version.
			/// </summary>
			private string referencedVersion;
			/// <summary>
			/// Whether the version is 'Production'.
			/// </summary>
			private bool isProduction;
			/// <summary>
			/// The connection info of the protocol.
			/// </summary>
			private System.Collections.Generic.IList<Skyline.DataMiner.Library.Common.IDmsConnectionInfo> connectionInfo = new System.Collections.Generic.List<Skyline.DataMiner.Library.Common.IDmsConnectionInfo>();
			/// <summary>
			/// Initializes a new instance of the <see cref = "DmsProtocol"/> class.
			/// </summary>
			/// <param name = "dms">The DataMiner System.</param>
			/// <param name = "name">The protocol name.</param>
			/// <param name = "version">The protocol version.</param>
			/// <param name = "type">The type of the protocol.</param>
			/// <param name = "referencedVersion">The protocol referenced version.</param>
			/// <exception cref = "ArgumentNullException"><paramref name = "dms"/> is <see langword = "null"/>.</exception>
			/// <exception cref = "ArgumentNullException"><paramref name = "name"/> is <see langword = "null"/>.</exception>
			/// <exception cref = "ArgumentNullException"><paramref name = "version"/> is <see langword = "null"/>.</exception>
			/// <exception cref = "ArgumentException"><paramref name = "name"/> is the empty string ("") or white space.</exception>
			/// <exception cref = "ArgumentException"><paramref name = "version"/> is the empty string ("") or white space.</exception>
			/// <exception cref = "ArgumentException"><paramref name = "version"/> is not 'Production' and <paramref name = "referencedVersion"/> is not the empty string ("") or white space.</exception>
			internal DmsProtocol(Skyline.DataMiner.Library.Common.IDms dms, string name, string version, Skyline.DataMiner.Library.Common.ProtocolType type = Skyline.DataMiner.Library.Common.ProtocolType.Undefined, string referencedVersion = "") : base(dms)
			{
				if (name == null)
				{
					throw new System.ArgumentNullException("name");
				}

				if (version == null)
				{
					throw new System.ArgumentNullException("version");
				}

				if (System.String.IsNullOrWhiteSpace(name))
				{
					throw new System.ArgumentException("The name of the protocol is the empty string (\"\") or white space.", "name");
				}

				if (System.String.IsNullOrWhiteSpace(version))
				{
					throw new System.ArgumentException("The version of the protocol is the empty string (\"\") or white space.", "version");
				}

				this.name = name;
				this.version = version;
				this.type = type;
				this.isProduction = CheckIsProduction(this.version);
				if (!this.isProduction && !System.String.IsNullOrWhiteSpace(referencedVersion))
				{
					throw new System.ArgumentException("The version of the protocol is not referenced version of the protocol is not the empty string (\"\") or white space.", "referencedVersion");
				}

				this.referencedVersion = referencedVersion;
			}

			/// <summary>
			/// Initializes a new instance of the <see cref = "DmsProtocol"/> class.
			/// </summary>
			/// <param name = "dms">The DataMiner system.</param>
			/// <param name = "infoMessage">The information message received from SLNet.</param>
			/// <param name = "requestedProduction">The version requested to SLNet.</param>
			/// <exception cref = "ArgumentNullException"><paramref name = "infoMessage"/> is <see langword = "null"/>.</exception>
			internal DmsProtocol(Skyline.DataMiner.Library.Common.IDms dms, Skyline.DataMiner.Net.Messages.GetProtocolInfoResponseMessage infoMessage, bool requestedProduction) : base(dms)
			{
				if (infoMessage == null)
				{
					throw new System.ArgumentNullException("infoMessage");
				}

				this.isProduction = requestedProduction;
				Parse(infoMessage);
			}

			/// <summary>
			/// Gets the protocol name.
			/// </summary>
			/// <value>The protocol name.</value>
			public string Name
			{
				get
				{
					return name;
				}
			}

			/// <summary>
			/// Gets the protocol version.
			/// </summary>
			/// <value>The protocol version.</value>
			public string Version
			{
				get
				{
					return version;
				}
			}

			/// <summary>
			/// Determines whether this protocol exists in the DataMiner System.
			/// </summary>
			/// <returns><c>true</c> if this protocol exists in the DataMiner System; otherwise, <c>false</c>.</returns>
			public override bool Exists()
			{
				return Dms.ProtocolExists(name, version);
			}

			/// <summary>
			/// Gets the alarm template with the specified name defined for this protocol.
			/// </summary>
			/// <param name = "templateName">The name of the alarm template.</param>
			/// <exception cref = "ArgumentNullException"><paramref name = "templateName"/> is <see langword = "null"/>.</exception>
			/// <exception cref = "ArgumentException"><paramref name = "templateName"/> is the empty string ("") or white space.</exception>
			/// <exception cref = "AlarmTemplateNotFoundException">No alarm template with the specified name was found.</exception>
			/// <returns>The alarm template with the specified name defined for this protocol.</returns>
			public Skyline.DataMiner.Library.Common.Templates.IDmsAlarmTemplate GetAlarmTemplate(string templateName)
			{
				Skyline.DataMiner.Net.Messages.GetAlarmTemplateMessage message = new Skyline.DataMiner.Net.Messages.GetAlarmTemplateMessage { AsOneObject = true, Protocol = this.Name, Version = this.Version, Template = templateName };
				Skyline.DataMiner.Net.Messages.AlarmTemplateEventMessage alarmTemplateEventMessage = (Skyline.DataMiner.Net.Messages.AlarmTemplateEventMessage)dms.Communication.SendSingleResponseMessage(message);
				if (alarmTemplateEventMessage == null)
				{
					throw new Skyline.DataMiner.Library.Common.AlarmTemplateNotFoundException(templateName, this);
				}

				if (alarmTemplateEventMessage.Type == Skyline.DataMiner.Net.Messages.AlarmTemplateType.Template)
				{
					return new Skyline.DataMiner.Library.Common.Templates.DmsStandaloneAlarmTemplate(dms, alarmTemplateEventMessage);
				}
				else if (alarmTemplateEventMessage.Type == Skyline.DataMiner.Net.Messages.AlarmTemplateType.Group)
				{
					return new Skyline.DataMiner.Library.Common.Templates.DmsAlarmTemplateGroup(dms, alarmTemplateEventMessage);
				}
				else
				{
					throw new System.NotSupportedException("Support for " + alarmTemplateEventMessage.Type + " has not yet been implemented.");
				}
			}

			/// <summary>
			/// Determines whether a standalone alarm template with the specified name exists for this protocol.
			/// </summary>
			/// <param name = "templateName">Name of the alarm template.</param>
			/// <exception cref = "ArgumentNullException"><paramref name = "templateName"/> is <see langword = "null"/>.</exception>
			/// <exception cref = "ArgumentException"><paramref name = "templateName"/> is the empty string ("") or white space.</exception>
			/// <returns><c>true</c> if a standalone alarm template with the specified name exists; otherwise, <c>false</c>.</returns>
			public bool StandaloneAlarmTemplateExists(string templateName)
			{
				bool exists = false;
				Skyline.DataMiner.Net.Messages.AlarmTemplateEventMessage template = GetAlarmTemplateSLNet(templateName);
				if (template != null && template.Type == Skyline.DataMiner.Net.Messages.AlarmTemplateType.Template)
				{
					exists = true;
				}

				return exists;
			}

			/// <summary>
			/// Returns a string that represents the current object.
			/// </summary>
			/// <returns>A string that represents the current object.</returns>
			public override string ToString()
			{
				return System.String.Format(System.Globalization.CultureInfo.InvariantCulture, "Protocol name: {0}, version: {1}", Name, Version);
			}

			/// <summary>
			/// Validate if <paramref name = "version"/> is 'Production'.
			/// </summary>
			/// <param name = "version">The version.</param>
			/// <returns>Whether <paramref name = "version"/> is 'Production'.</returns>
			internal static bool CheckIsProduction(string version)
			{
				return System.String.Equals(version, Production, System.StringComparison.OrdinalIgnoreCase);
			}

			/// <summary>
			/// Loads the object.
			/// </summary>
			/// <exception cref = "ProtocolNotFoundException">No protocol with the specified name and version exists in the DataMiner system.</exception>
			internal override void Load()
			{
				isProduction = CheckIsProduction(version);
				Skyline.DataMiner.Net.Messages.GetProtocolMessage getProtocolMessage = new Skyline.DataMiner.Net.Messages.GetProtocolMessage { Protocol = name, Version = version };
				Skyline.DataMiner.Net.Messages.GetProtocolInfoResponseMessage protocolInfo = (Skyline.DataMiner.Net.Messages.GetProtocolInfoResponseMessage)Communication.SendSingleResponseMessage(getProtocolMessage);
				if (protocolInfo != null)
				{
					Parse(protocolInfo);
				}
				else
				{
					throw new Skyline.DataMiner.Library.Common.ProtocolNotFoundException(name, version);
				}
			}

			/// <summary>
			/// Parses the <see cref = "GetProtocolInfoResponseMessage"/> message.
			/// </summary>
			/// <param name = "protocolInfo">The protocol information.</param>
			private void Parse(Skyline.DataMiner.Net.Messages.GetProtocolInfoResponseMessage protocolInfo)
			{
				IsLoaded = true;
				name = protocolInfo.Name;
				type = (Skyline.DataMiner.Library.Common.ProtocolType)protocolInfo.ProtocolType;
				if (isProduction)
				{
					version = Production;
					referencedVersion = protocolInfo.Version;
				}
				else
				{
					version = protocolInfo.Version;
					referencedVersion = System.String.Empty;
				}

				ParseConnectionInfo(protocolInfo);
			}

			/// <summary>
			/// Parses the <see cref = "GetProtocolInfoResponseMessage"/> message.
			/// </summary>
			/// <param name = "protocolInfo">The protocol information.</param>
			private void ParseConnectionInfo(Skyline.DataMiner.Net.Messages.GetProtocolInfoResponseMessage protocolInfo)
			{
				System.Collections.Generic.List<Skyline.DataMiner.Library.Common.DmsConnectionInfo> info = new System.Collections.Generic.List<Skyline.DataMiner.Library.Common.DmsConnectionInfo>();
				info.Add(new Skyline.DataMiner.Library.Common.DmsConnectionInfo(System.String.Empty, Skyline.DataMiner.Library.Common.EnumMapper.ConvertStringToConnectionType(protocolInfo.Type)));
				if (protocolInfo.AdvancedTypes != null && protocolInfo.AdvancedTypes.Length > 0 && !System.String.IsNullOrWhiteSpace(protocolInfo.AdvancedTypes))
				{
					string[] split = protocolInfo.AdvancedTypes.Split(';');
					foreach (string part in split)
					{
						if (part.Contains(":"))
						{
							string[] connectionSplit = part.Split(':');
							Skyline.DataMiner.Library.Common.ConnectionType connectionType = Skyline.DataMiner.Library.Common.EnumMapper.ConvertStringToConnectionType(connectionSplit[0]);
							string connectionName = connectionSplit[1];
							info.Add(new Skyline.DataMiner.Library.Common.DmsConnectionInfo(connectionName, connectionType));
						}
						else
						{
							Skyline.DataMiner.Library.Common.ConnectionType connectionType = Skyline.DataMiner.Library.Common.EnumMapper.ConvertStringToConnectionType(part);
							string connectionName = System.String.Empty;
							info.Add(new Skyline.DataMiner.Library.Common.DmsConnectionInfo(connectionName, connectionType));
						}
					}
				}

				connectionInfo = info.ToArray();
			}

			/// <summary>
			/// Gets the alarm template via SLNet.
			/// </summary>
			/// <param name = "templateName">The name of the alarm template.</param>
			/// <exception cref = "ArgumentNullException"><paramref name = "templateName"/> is <see langword = "null"/>.</exception>
			/// <exception cref = "ArgumentException"><paramref name = "templateName"/> is the empty string ("") or white space.</exception>
			/// <returns>The AlarmTemplateEventMessage object.</returns>
			private Skyline.DataMiner.Net.Messages.AlarmTemplateEventMessage GetAlarmTemplateSLNet(string templateName)
			{
				if (templateName == null)
				{
					throw new System.ArgumentNullException("templateName");
				}

				if (System.String.IsNullOrWhiteSpace(templateName))
				{
					throw new System.ArgumentException("Provided template name must not be the empty string (\"\") or white space", "templateName");
				}

				Skyline.DataMiner.Net.Messages.GetAlarmTemplateMessage message = new Skyline.DataMiner.Net.Messages.GetAlarmTemplateMessage { AsOneObject = true, Protocol = this.Name, Template = templateName, Version = this.Version };
				return (Skyline.DataMiner.Net.Messages.AlarmTemplateEventMessage)Dms.Communication.SendSingleResponseMessage(message);
			}
		}

		/// <summary>
		/// DataMiner protocol interface.
		/// </summary>
		public interface IDmsProtocol : Skyline.DataMiner.Library.Common.IDmsObject
		{
			/// <summary>
			/// Gets the protocol name.
			/// </summary>
			/// <value>The protocol name.</value>
			string Name
			{
				get;
			}

			/// <summary>
			/// Gets the protocol version.
			/// </summary>
			/// <value>The protocol version.</value>
			string Version
			{
				get;
			}

			/// <summary>
			/// Gets the alarm template with the specified name defined for this protocol.
			/// </summary>
			/// <param name = "templateName">The name of the alarm template.</param>
			/// <exception cref = "ArgumentNullException"><paramref name = "templateName"/> is <see langword = "null"/>.</exception>
			/// <exception cref = "ArgumentException"><paramref name = "templateName"/> is the empty string ("") or white space.</exception>
			/// <exception cref = "AlarmTemplateNotFoundException">No alarm template with the specified name was found.</exception>
			/// <returns>The alarm template with the specified name defined for this protocol.</returns>
			Skyline.DataMiner.Library.Common.Templates.IDmsAlarmTemplate GetAlarmTemplate(string templateName);
			/// <summary>
			/// Determines whether a standalone alarm template with the specified name exists for this protocol.
			/// </summary>
			/// <param name = "templateName">Name of the alarm template.</param>
			/// <exception cref = "ArgumentNullException"><paramref name = "templateName"/> is <see langword = "null"/>.</exception>
			/// <exception cref = "ArgumentException"><paramref name = "templateName"/> is the empty string ("") or white space.</exception>
			/// <returns><c>true</c> if a standalone alarm template with the specified name exists; otherwise, <c>false</c>.</returns>
			bool StandaloneAlarmTemplateExists(string templateName);
		}

		/// <summary>
		/// Represents the DataMiner Scheduler component.
		/// </summary>
		internal class DmsScheduler : Skyline.DataMiner.Library.Common.IDmsScheduler
		{
			private readonly Skyline.DataMiner.Library.Common.IDma myDma;
			/// <summary>
			/// Initializes a new instance of the <see cref = "DmsScheduler"/> class.
			/// </summary>
			/// <param name = "agent">The agent to which this scheduler component belongs to.</param>
			public DmsScheduler(Skyline.DataMiner.Library.Common.IDma agent)
			{
				myDma = agent;
			}
		}

		/// <summary>
		/// Represents the DataMiner Scheduler component.
		/// </summary>
		public interface IDmsScheduler
		{
		}

		namespace Templates
		{
			/// <summary>
			/// Base class for standalone alarm templates and alarm template groups.
			/// </summary>
			internal abstract class DmsAlarmTemplate : Skyline.DataMiner.Library.Common.Templates.DmsTemplate, Skyline.DataMiner.Library.Common.Templates.IDmsAlarmTemplate
			{
				/// <summary>
				/// Initializes a new instance of the <see cref = "DmsAlarmTemplate"/> class.
				/// </summary>
				/// <param name = "dms">Object implementing the <see cref = "IDms"/> interface.</param>
				/// <param name = "name">The name of the alarm template.</param>
				/// <param name = "protocol">Instance of the protocol.</param>
				/// <exception cref = "ArgumentNullException"><paramref name = "dms"/> is <see langword = "null"/>.</exception>
				/// <exception cref = "ArgumentNullException"><paramref name = "name"/> is <see langword = "null"/>.</exception>
				/// <exception cref = "ArgumentNullException"><paramref name = "protocol"/> is <see langword = "null"/>.</exception>
				/// <exception cref = "ArgumentException"><paramref name = "name"/> is the empty string ("") or white space.</exception>
				protected DmsAlarmTemplate(Skyline.DataMiner.Library.Common.IDms dms, string name, Skyline.DataMiner.Library.Common.IDmsProtocol protocol) : base(dms, name, protocol)
				{
				}

				/// <summary>
				/// Initializes a new instance of the <see cref = "DmsAlarmTemplate"/> class.
				/// </summary>
				/// <param name = "dms">Object implementing the <see cref = "IDms"/> interface.</param>
				/// <param name = "name">The name of the alarm template.</param>
				/// <param name = "protocolName">The name of the protocol.</param>
				/// <param name = "protocolVersion">The version of the protocol.</param>
				/// <exception cref = "ArgumentNullException"><paramref name = "dms"/> is <see langword = "null"/>.</exception>
				/// <exception cref = "ArgumentNullException"><paramref name = "name"/> is <see langword = "null"/>.</exception>
				/// <exception cref = "ArgumentNullException"><paramref name = "protocolName"/> is <see langword = "null"/>.</exception>
				/// <exception cref = "ArgumentNullException"><paramref name = "protocolVersion"/> is <see langword = "null"/>.</exception>
				/// <exception cref = "ArgumentException"><paramref name = "name"/> is the empty string ("") or white space.</exception>
				/// <exception cref = "ArgumentException"><paramref name = "protocolName"/> is the empty string ("") or white space.</exception>
				/// <exception cref = "ArgumentException"><paramref name = "protocolVersion"/> is the empty string ("") or white space.</exception>
				protected DmsAlarmTemplate(Skyline.DataMiner.Library.Common.IDms dms, string name, string protocolName, string protocolVersion) : base(dms, name, protocolName, protocolVersion)
				{
				}

				/// <summary>
				/// Loads all the data and properties found related to the alarm template.
				/// </summary>
				/// <exception cref = "TemplateNotFoundException">The template does not exist in the DataMiner system.</exception>
				internal override void Load()
				{
					Skyline.DataMiner.Net.Messages.GetAlarmTemplateMessage message = new Skyline.DataMiner.Net.Messages.GetAlarmTemplateMessage { AsOneObject = true, Protocol = Protocol.Name, Version = Protocol.Version, Template = Name };
					Skyline.DataMiner.Net.Messages.AlarmTemplateEventMessage response = (Skyline.DataMiner.Net.Messages.AlarmTemplateEventMessage)Dms.Communication.SendSingleResponseMessage(message);
					if (response != null)
					{
						Parse(response);
					}
					else
					{
						throw new Skyline.DataMiner.Library.Common.TemplateNotFoundException(Name, Protocol.Name, Protocol.Version);
					}
				}

				/// <summary>
				/// Parses the alarm template event message.
				/// </summary>
				/// <param name = "message">The message received from SLNet.</param>
				internal abstract void Parse(Skyline.DataMiner.Net.Messages.AlarmTemplateEventMessage message);
			}

			/// <summary>
			/// Represents an alarm template group.
			/// </summary>
			internal class DmsAlarmTemplateGroup : Skyline.DataMiner.Library.Common.Templates.DmsAlarmTemplate, Skyline.DataMiner.Library.Common.Templates.IDmsAlarmTemplateGroup
			{
				/// <summary>
				/// The entries of the alarm group.
				/// </summary>
				private readonly System.Collections.Generic.List<Skyline.DataMiner.Library.Common.Templates.IDmsAlarmTemplateGroupEntry> entries = new System.Collections.Generic.List<Skyline.DataMiner.Library.Common.Templates.IDmsAlarmTemplateGroupEntry>();
				/// <summary>
				/// Initializes a new instance of the <see cref = "DmsAlarmTemplateGroup"/> class.
				/// </summary>
				/// <param name = "dms">Object implementing the <see cref = "IDms"/> interface.</param>
				/// <param name = "name">The name of the alarm template.</param>
				/// <param name = "protocol">The protocol this alarm template group corresponds with.</param>
				/// <exception cref = "ArgumentNullException"><paramref name = "dms"/> is <see langword = "null"/>.</exception>
				/// <exception cref = "ArgumentNullException"><paramref name = "name"/> is <see langword = "null"/>.</exception>
				/// <exception cref = "ArgumentNullException"><paramref name = "protocol"/> is <see langword = "null"/>.</exception>
				/// <exception cref = "ArgumentException"><paramref name = "name"/> is the empty string ("") or white space.</exception>
				internal DmsAlarmTemplateGroup(Skyline.DataMiner.Library.Common.IDms dms, string name, Skyline.DataMiner.Library.Common.IDmsProtocol protocol) : base(dms, name, protocol)
				{
					IsLoaded = false;
				}

				/// <summary>
				/// Initializes a new instance of the <see cref = "DmsAlarmTemplateGroup"/> class.
				/// </summary>
				/// <param name = "dms">Instance of <see cref = "Dms"/>.</param>
				/// <param name = "alarmTemplateEventMessage">An instance of AlarmTemplateEventMessage.</param>
				/// <exception cref = "ArgumentNullException"><paramref name = "dms"/> is <see langword = "null"/>.</exception>
				/// <exception cref = "ArgumentException"><paramref name = "alarmTemplateEventMessage"/> is invalid.</exception>
				internal DmsAlarmTemplateGroup(Skyline.DataMiner.Library.Common.IDms dms, Skyline.DataMiner.Net.Messages.AlarmTemplateEventMessage alarmTemplateEventMessage) : base(dms, alarmTemplateEventMessage.Name, alarmTemplateEventMessage.Protocol, alarmTemplateEventMessage.Version)
				{
					IsLoaded = true;
					foreach (Skyline.DataMiner.Net.Messages.AlarmTemplateGroupEntry entry in alarmTemplateEventMessage.GroupEntries)
					{
						Skyline.DataMiner.Library.Common.Templates.IDmsAlarmTemplate template = Protocol.GetAlarmTemplate(entry.Name);
						entries.Add(new Skyline.DataMiner.Library.Common.Templates.DmsAlarmTemplateGroupEntry(template, entry.IsEnabled, entry.IsScheduled));
					}
				}

				/// <summary>
				/// Determines whether this alarm template exists in the DataMiner System.
				/// </summary>
				/// <returns><c>true</c> if the alarm template exists in the DataMiner System; otherwise, <c>false</c>.</returns>
				public override bool Exists()
				{
					bool exists = false;
					Skyline.DataMiner.Net.Messages.AlarmTemplateEventMessage template = GetAlarmTemplate();
					if (template != null && template.Type == Skyline.DataMiner.Net.Messages.AlarmTemplateType.Group)
					{
						exists = true;
					}

					return exists;
				}

				/// <summary>
				/// Returns a string that represents the current object.
				/// </summary>
				/// <returns>A string that represents the current object.</returns>
				public override string ToString()
				{
					return System.String.Format(System.Globalization.CultureInfo.InvariantCulture, "Template Group Name: {0}, Protocol Name: {1}, Protocol Version: {2}", Name, Protocol.Name, Protocol.Version);
				}

				/// <summary>
				/// Parses the alarm template event message.
				/// </summary>
				/// <param name = "message">The message received from the SLNet process.</param>
				internal override void Parse(Skyline.DataMiner.Net.Messages.AlarmTemplateEventMessage message)
				{
					IsLoaded = true;
					entries.Clear();
					foreach (Skyline.DataMiner.Net.Messages.AlarmTemplateGroupEntry entry in message.GroupEntries)
					{
						Skyline.DataMiner.Library.Common.Templates.IDmsAlarmTemplate template = Protocol.GetAlarmTemplate(entry.Name);
						entries.Add(new Skyline.DataMiner.Library.Common.Templates.DmsAlarmTemplateGroupEntry(template, entry.IsEnabled, entry.IsScheduled));
					}
				}

				/// <summary>
				/// Gets the alarm template from the SLNet process.
				/// </summary>
				/// <returns>The alarm template.</returns>
				private Skyline.DataMiner.Net.Messages.AlarmTemplateEventMessage GetAlarmTemplate()
				{
					Skyline.DataMiner.Net.Messages.GetAlarmTemplateMessage message = new Skyline.DataMiner.Net.Messages.GetAlarmTemplateMessage { AsOneObject = true, Protocol = Protocol.Name, Version = Protocol.Version, Template = Name };
					Skyline.DataMiner.Net.Messages.AlarmTemplateEventMessage cachedAlarmTemplateMessage = (Skyline.DataMiner.Net.Messages.AlarmTemplateEventMessage)Dms.Communication.SendSingleResponseMessage(message);
					return cachedAlarmTemplateMessage;
				}
			}

			/// <summary>
			/// Represents an alarm group entry.
			/// </summary>
			internal class DmsAlarmTemplateGroupEntry : Skyline.DataMiner.Library.Common.Templates.IDmsAlarmTemplateGroupEntry
			{
				/// <summary>
				/// The template which is an entry of the alarm group.
				/// </summary>
				private readonly Skyline.DataMiner.Library.Common.Templates.IDmsAlarmTemplate template;
				/// <summary>
				/// Specifies whether this entry is enabled.
				/// </summary>
				private readonly bool isEnabled;
				/// <summary>
				/// Specifies whether this entry is scheduled.
				/// </summary>
				private readonly bool isScheduled;
				/// <summary>
				/// Initializes a new instance of the <see cref = "DmsAlarmTemplateGroupEntry"/> class.
				/// </summary>
				/// <param name = "template">The alarm template.</param>
				/// <param name = "isEnabled">Specifies if the entry is enabled.</param>
				/// <param name = "isScheduled">Specifies if the entry is scheduled.</param>
				internal DmsAlarmTemplateGroupEntry(Skyline.DataMiner.Library.Common.Templates.IDmsAlarmTemplate template, bool isEnabled, bool isScheduled)
				{
					if (template == null)
					{
						throw new System.ArgumentNullException("template");
					}

					this.template = template;
					this.isEnabled = isEnabled;
					this.isScheduled = isScheduled;
				}

				/// <summary>
				/// Returns a string that represents the current object.
				/// </summary>
				/// <returns>A string that represents the current object.</returns>
				public override string ToString()
				{
					return System.String.Format(System.Globalization.CultureInfo.InvariantCulture, "Alarm template group entry:{0}", template.Name);
				}
			}

			/// <summary>
			/// Represents a standalone alarm template.
			/// </summary>
			internal class DmsStandaloneAlarmTemplate : Skyline.DataMiner.Library.Common.Templates.DmsAlarmTemplate, Skyline.DataMiner.Library.Common.Templates.IDmsStandaloneAlarmTemplate
			{
				/// <summary>
				/// The description of the alarm definition.
				/// </summary>
				private string description;
				/// <summary>
				/// Indicates whether this alarm template is used in a group.
				/// </summary>
				private bool isUsedInGroup;
				/// <summary>
				/// Initializes a new instance of the <see cref = "DmsStandaloneAlarmTemplate"/> class.
				/// </summary>
				/// <param name = "dms">Object implementing the <see cref = "IDms"/> interface.</param>
				/// <param name = "name">The name of the alarm template.</param>
				/// <param name = "protocol">The protocol this standalone alarm template corresponds with.</param>
				/// <exception cref = "ArgumentNullException"><paramref name = "dms"/> is <see langword = "null"/>.</exception>
				/// <exception cref = "ArgumentNullException"><paramref name = "name"/> is <see langword = "null"/>.</exception>
				/// <exception cref = "ArgumentNullException"><paramref name = "protocol"/> is <see langword = "null"/>.</exception>
				/// <exception cref = "ArgumentException"><paramref name = "name"/> is the empty string ("") or white space.</exception>
				internal DmsStandaloneAlarmTemplate(Skyline.DataMiner.Library.Common.IDms dms, string name, Skyline.DataMiner.Library.Common.IDmsProtocol protocol) : base(dms, name, protocol)
				{
					IsLoaded = false;
				}

				/// <summary>
				/// Initializes a new instance of the <see cref = "DmsStandaloneAlarmTemplate"/> class.
				/// </summary>
				/// <param name = "dms">The DataMiner system reference.</param>
				/// <param name = "alarmTemplateEventMessage">An instance of AlarmTemplateEventMessage.</param>
				/// <exception cref = "ArgumentNullException"><paramref name = "dms"/> is <see langword = "null"/>.</exception>
				/// <exception cref = "ArgumentException"><paramref name = "dms"/> is invalid.</exception>
				internal DmsStandaloneAlarmTemplate(Skyline.DataMiner.Library.Common.IDms dms, Skyline.DataMiner.Net.Messages.AlarmTemplateEventMessage alarmTemplateEventMessage) : base(dms, alarmTemplateEventMessage.Name, alarmTemplateEventMessage.Protocol, alarmTemplateEventMessage.Version)
				{
					IsLoaded = true;
					description = alarmTemplateEventMessage.Description;
					isUsedInGroup = alarmTemplateEventMessage.IsUsedInGroup;
				}

				/// <summary>
				/// Determines whether this alarm template exists in the DataMiner System.
				/// </summary>
				/// <returns><c>true</c> if the alarm template exists in the DataMiner System; otherwise, <c>false</c>.</returns>
				public override bool Exists()
				{
					return Protocol.StandaloneAlarmTemplateExists(Name);
				}

				/// <summary>
				/// Returns a string that represents the current object.
				/// </summary>
				/// <returns>A string that represents the current object.</returns>
				public override string ToString()
				{
					return System.String.Format(System.Globalization.CultureInfo.InvariantCulture, "Alarm Template Name: {0}, Protocol Name: {1}, Protocol Version: {2}", Name, Protocol.Name, Protocol.Version);
				}

				/// <summary>
				/// Parses the alarm template event message.
				/// </summary>
				/// <param name = "message">The message received from SLNet.</param>
				internal override void Parse(Skyline.DataMiner.Net.Messages.AlarmTemplateEventMessage message)
				{
					IsLoaded = true;
					description = message.Description;
					isUsedInGroup = message.IsUsedInGroup;
				}
			}

			/// <summary>
			/// Represents an alarm template.
			/// </summary>
			internal abstract class DmsTemplate : Skyline.DataMiner.Library.Common.DmsObject
			{
				/// <summary>
				/// Alarm template name.
				/// </summary>
				private readonly string name;
				/// <summary>
				/// The protocol this alarm template corresponds with.
				/// </summary>
				private readonly Skyline.DataMiner.Library.Common.IDmsProtocol protocol;
				/// <summary>
				/// Initializes a new instance of the <see cref = "DmsTemplate"/> class.
				/// </summary>
				/// <param name = "dms">Object implementing the <see cref = "IDms"/> interface.</param>
				/// <param name = "name">The name of the alarm template.</param>
				/// <param name = "protocol">Instance of the protocol.</param>
				/// <exception cref = "ArgumentNullException"><paramref name = "dms"/> is <see langword = "null"/>.</exception>
				/// <exception cref = "ArgumentNullException"><paramref name = "name"/> is <see langword = "null"/>.</exception>
				/// <exception cref = "ArgumentNullException"><paramref name = "protocol"/> is <see langword = "null"/>.</exception>
				/// <exception cref = "ArgumentException"><paramref name = "name"/> is the empty string ("") or white space.</exception>
				protected DmsTemplate(Skyline.DataMiner.Library.Common.IDms dms, string name, Skyline.DataMiner.Library.Common.IDmsProtocol protocol) : base(dms)
				{
					if (name == null)
					{
						throw new System.ArgumentNullException("name");
					}

					if (protocol == null)
					{
						throw new System.ArgumentNullException("protocol");
					}

					if (System.String.IsNullOrWhiteSpace(name))
					{
						throw new System.ArgumentException("The name of the template is the empty string (\"\") or white space.");
					}

					this.name = name;
					this.protocol = protocol;
				}

				/// <summary>
				/// Initializes a new instance of the <see cref = "DmsTemplate"/> class.
				/// </summary>
				/// <param name = "dms">The DataMiner System reference.</param>
				/// <param name = "name">The template name.</param>
				/// <param name = "protocolName">The name of the protocol.</param>
				/// <param name = "protocolVersion">The version of the protocol.</param>
				/// <exception cref = "ArgumentNullException"><paramref name = "dms"/> is <see langword = "null"/>.</exception>
				/// <exception cref = "ArgumentNullException"><paramref name = "name"/> is <see langword = "null"/>.</exception>
				/// <exception cref = "ArgumentNullException"><paramref name = "protocolName"/> is <see langword = "null"/>.</exception>
				/// <exception cref = "ArgumentNullException"><paramref name = "protocolVersion"/> is <see langword = "null"/>.</exception>
				/// <exception cref = "ArgumentException"><paramref name = "name"/> is the empty string ("") or white space.</exception>
				/// <exception cref = "ArgumentException"><paramref name = "protocolName"/> is the empty string ("") or white space.</exception>
				/// <exception cref = "ArgumentException"><paramref name = "protocolVersion"/> is the empty string ("") or white space.</exception>
				protected DmsTemplate(Skyline.DataMiner.Library.Common.IDms dms, string name, string protocolName, string protocolVersion) : base(dms)
				{
					if (name == null)
					{
						throw new System.ArgumentNullException("name");
					}

					if (protocolName == null)
					{
						throw new System.ArgumentNullException("protocolName");
					}

					if (protocolVersion == null)
					{
						throw new System.ArgumentNullException("protocolVersion");
					}

					if (System.String.IsNullOrWhiteSpace(name))
					{
						throw new System.ArgumentException("The name of the template is the empty string(\"\") or white space.", "name");
					}

					if (System.String.IsNullOrWhiteSpace(protocolName))
					{
						throw new System.ArgumentException("The name of the protocol is the empty string (\"\") or white space.", "protocolName");
					}

					if (System.String.IsNullOrWhiteSpace(protocolVersion))
					{
						throw new System.ArgumentException("The version of the protocol is the empty string (\"\") or white space.", "protocolVersion");
					}

					this.name = name;
					protocol = new Skyline.DataMiner.Library.Common.DmsProtocol(dms, protocolName, protocolVersion);
				}

				/// <summary>
				/// Gets the template name.
				/// </summary>
				public string Name
				{
					get
					{
						return name;
					}
				}

				/// <summary>
				/// Gets the protocol this template corresponds with.
				/// </summary>
				public Skyline.DataMiner.Library.Common.IDmsProtocol Protocol
				{
					get
					{
						return protocol;
					}
				}
			}

			/// <summary>
			/// Represents a trend template.
			/// </summary>
			internal class DmsTrendTemplate : Skyline.DataMiner.Library.Common.Templates.DmsTemplate, Skyline.DataMiner.Library.Common.Templates.IDmsTrendTemplate
			{
				/// <summary>
				/// Initializes a new instance of the <see cref = "DmsTrendTemplate"/> class.
				/// </summary>
				/// <param name = "dms">Object implementing the <see cref = "IDms"/> interface.</param>
				/// <param name = "name">The name of the alarm template.</param>
				/// <param name = "protocol">The instance of the protocol.</param>
				/// <exception cref = "ArgumentNullException">Dms is <see langword = "null"/>.</exception>
				/// <exception cref = "ArgumentNullException">Name is <see langword = "null"/>.</exception>
				/// <exception cref = "ArgumentNullException">Protocol is <see langword = "null"/>.</exception>
				/// <exception cref = "ArgumentException"><paramref name = "name"/> is the empty string ("") or white space.</exception>
				internal DmsTrendTemplate(Skyline.DataMiner.Library.Common.IDms dms, string name, Skyline.DataMiner.Library.Common.IDmsProtocol protocol) : base(dms, name, protocol)
				{
					IsLoaded = true;
				}

				/// <summary>
				/// Initializes a new instance of the <see cref = "DmsTrendTemplate"/> class.
				/// </summary>
				/// <param name = "dms">Object implementing the <see cref = "IDms"/> interface.</param>
				/// <param name = "templateInfo">The template info received by SLNet.</param>
				/// <exception cref = "ArgumentNullException"><paramref name = "dms"/> is <see langword = "null"/>.</exception>
				/// <exception cref = "ArgumentNullException">name is <see langword = "null"/>.</exception>
				/// <exception cref = "ArgumentNullException">protocolName is <see langword = "null"/>.</exception>
				/// <exception cref = "ArgumentNullException">protocolVersion is <see langword = "null"/>.</exception>
				/// <exception cref = "ArgumentException">name is the empty string ("") or white space.</exception>
				/// <exception cref = "ArgumentException">ProtocolName is the empty string ("") or white space.</exception>
				/// <exception cref = "ArgumentException">ProtocolVersion is the empty string ("") or white space.</exception>
				internal DmsTrendTemplate(Skyline.DataMiner.Library.Common.IDms dms, Skyline.DataMiner.Net.Messages.GetTrendingTemplateInfoResponseMessage templateInfo) : base(dms, templateInfo.Name, templateInfo.Protocol, templateInfo.Version)
				{
					IsLoaded = true;
				}

				/// <summary>
				/// Initializes a new instance of the <see cref = "DmsTrendTemplate"/> class.
				/// </summary>
				/// <param name = "dms">Object implementing the <see cref = "IDms"/> interface.</param>
				/// <param name = "templateInfo">The template info received by SLNet.</param>
				/// <exception cref = "ArgumentNullException"><paramref name = "dms"/> is <see langword = "null"/>.</exception>
				/// <exception cref = "ArgumentNullException">Name is <see langword = "null"/>.</exception>
				/// <exception cref = "ArgumentNullException">ProtocolName is <see langword = "null"/>.</exception>
				/// <exception cref = "ArgumentNullException">ProtocolVersion is <see langword = "null"/>.</exception>
				/// <exception cref = "ArgumentException">Name is the empty string ("") or white space.</exception>
				/// <exception cref = "ArgumentException">ProtocolName is the empty string ("") or white space.</exception>
				/// <exception cref = "ArgumentException">ProtocolVersion is the empty string ("") or white space.</exception>
				internal DmsTrendTemplate(Skyline.DataMiner.Library.Common.IDms dms, Skyline.DataMiner.Net.Messages.TrendTemplateMetaInfo templateInfo) : base(dms, templateInfo.Name, templateInfo.ProtocolName, templateInfo.ProtocolVersion)
				{
					IsLoaded = true;
				}

				/// <summary>
				/// Determines whether this trend template exists in the DataMiner System.
				/// </summary>
				/// <returns><c>true</c> if the trend template exists in the DataMiner System; otherwise, <c>false</c>.</returns>
				public override bool Exists()
				{
					Skyline.DataMiner.Net.Messages.GetTrendingTemplateInfoMessage message = new Skyline.DataMiner.Net.Messages.GetTrendingTemplateInfoMessage { Protocol = Protocol.Name, Version = Protocol.Version, Template = Name };
					Skyline.DataMiner.Net.Messages.GetTrendingTemplateInfoResponseMessage response = (Skyline.DataMiner.Net.Messages.GetTrendingTemplateInfoResponseMessage)Dms.Communication.SendSingleResponseMessage(message);
					return response != null;
				}

				/// <summary>
				/// Returns a string that represents the current object.
				/// </summary>
				/// <returns>A string that represents the current object.</returns>
				public override string ToString()
				{
					return System.String.Format(System.Globalization.CultureInfo.InvariantCulture, "Trend Template Name: {0}, Protocol Name: {1}, Protocol Version: {2}", Name, Protocol.Name, Protocol.Version);
				}

				/// <summary>
				/// Loads this object.
				/// </summary>
				internal override void Load()
				{
				}
			}

			/// <summary>
			/// DataMiner alarm template interface.
			/// </summary>
			public interface IDmsAlarmTemplate : Skyline.DataMiner.Library.Common.Templates.IDmsTemplate
			{
			}

			/// <summary>
			/// DataMiner alarm template group interface.
			/// </summary>
			public interface IDmsAlarmTemplateGroup : Skyline.DataMiner.Library.Common.Templates.IDmsAlarmTemplate
			{
			}

			/// <summary>
			/// DataMiner alarm template group entry interface.
			/// </summary>
			public interface IDmsAlarmTemplateGroupEntry
			{
			}

			/// <summary>
			/// DataMiner standalone alarm template interface.
			/// </summary>
			public interface IDmsStandaloneAlarmTemplate : Skyline.DataMiner.Library.Common.Templates.IDmsAlarmTemplate
			{
			}

			/// <summary>
			/// DataMiner template interface.
			/// </summary>
			public interface IDmsTemplate : Skyline.DataMiner.Library.Common.IDmsObject
			{
				/// <summary>
				/// Gets the template name.
				/// </summary>
				string Name
				{
					get;
				}

				/// <summary>
				/// Gets the protocol this template corresponds with.
				/// </summary>
				Skyline.DataMiner.Library.Common.IDmsProtocol Protocol
				{
					get;
				}
			}

			/// <summary>
			/// DataMiner trend template interface.
			/// </summary>
			public interface IDmsTrendTemplate : Skyline.DataMiner.Library.Common.Templates.IDmsTemplate
			{
			}
		}

		internal static class Logger
		{
			private const long SizeLimit = 3 * 1024 * 1024;
			private const string LogFileName = @"C:\Skyline DataMiner\logging\ClassLibrary.txt";
			private const string LogPositionPlaceholder = "**********";
			private const int PlaceHolderSize = 10;
			private static long logPositionPlaceholderStart = -1;
			private static System.Threading.Mutex loggerMutex = new System.Threading.Mutex(false, "clpMutex");
			public static void Log(string message)
			{
				try
				{
					loggerMutex.WaitOne();
					string logPrefix = System.DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss.fff") + "|";
					long messageByteCount = System.Text.Encoding.UTF8.GetByteCount(message);
					// Safeguard for large messages.
					if (messageByteCount > SizeLimit)
					{
						message = "WARNING: message \"" + message.Substring(0, 100) + " not logged as it is too large (over " + SizeLimit + " bytes).";
					}

					long limit = SizeLimit / 2; // Safeguard: limit messages. If safeguard removed, the limit would be: SizeLimit - placeholder size - prefix length - 4 (2 * CR LF).
					if (messageByteCount > limit)
					{
						long overhead = messageByteCount - limit;
						int partToRemove = (int)overhead / 4; // In worst case, each char takes 4 bytes.
						if (partToRemove == 0)
						{
							partToRemove = 1;
						}

						while (messageByteCount > limit)
						{
							message = message.Substring(0, message.Length - partToRemove);
							messageByteCount = System.Text.Encoding.UTF8.GetByteCount(message);
						}
					}

					int byteCount = System.Text.Encoding.UTF8.GetByteCount(message);
					long positionOfPlaceHolder = GetPlaceHolderPosition();
					System.IO.Stream fileStream = null;
					try
					{
						fileStream = new System.IO.FileStream(LogFileName, System.IO.FileMode.OpenOrCreate, System.IO.FileAccess.ReadWrite);
						using (System.IO.StreamWriter sw = new System.IO.StreamWriter(fileStream))
						{
							fileStream = null;
							if (positionOfPlaceHolder == -1)
							{
								sw.BaseStream.Position = 0;
								sw.Write(logPrefix);
								sw.WriteLine(message);
								logPositionPlaceholderStart = byteCount + logPrefix.Length;
								sw.WriteLine(LogPositionPlaceholder);
							}
							else
							{
								sw.BaseStream.Position = positionOfPlaceHolder;
								if (positionOfPlaceHolder + byteCount + 4 + PlaceHolderSize > SizeLimit)
								{
									// Overwrite previous placeholder.
									byte[] placeholder = System.Text.Encoding.UTF8.GetBytes("          ");
									sw.BaseStream.Write(placeholder, 0, placeholder.Length);
									sw.BaseStream.Position = 0;
								}

								sw.Write(logPrefix);
								sw.WriteLine(message);
								sw.Flush();
								logPositionPlaceholderStart = sw.BaseStream.Position;
								sw.WriteLine(LogPositionPlaceholder);
							}
						}
					}
					finally
					{
						if (fileStream != null)
						{
							fileStream.Dispose();
						}
					}
				}
				catch
				{
					// Do nothing.
				}
				finally
				{
					loggerMutex.ReleaseMutex();
				}
			}

			private static long SetToStartOfLine(System.IO.StreamReader streamReader, long startPosition)
			{
				System.IO.Stream stream = streamReader.BaseStream;
				for (long position = startPosition - 1; position > 0; position--)
				{
					stream.Position = position;
					if (stream.ReadByte() == '\n')
					{
						return position + 1;
					}
				}

				return 0;
			}

			private static long GetPlaceHolderPosition()
			{
				long result = -1;
				System.IO.Stream fileStream = null;
				try
				{
					fileStream = System.IO.File.Open(LogFileName, System.IO.FileMode.OpenOrCreate, System.IO.FileAccess.ReadWrite, System.IO.FileShare.ReadWrite);
					using (System.IO.StreamReader streamReader = new System.IO.StreamReader(fileStream))
					{
						fileStream = null;
						streamReader.DiscardBufferedData();
						long startOfLinePosition = SetToStartOfLine(streamReader, logPositionPlaceholderStart);
						streamReader.DiscardBufferedData();
						streamReader.BaseStream.Position = startOfLinePosition;
						string line;
						long postionInFile = startOfLinePosition;
						while ((line = streamReader.ReadLine()) != null)
						{
							if (line == LogPositionPlaceholder)
							{
								streamReader.DiscardBufferedData();
								result = postionInFile;
								break;
							}
							else
							{
								postionInFile = postionInFile + System.Text.Encoding.UTF8.GetByteCount(line) + 2;
							}
						}

						// If this point is reached, it means the placeholder was still not found.
						if (result == -1 && startOfLinePosition > 0)
						{
							streamReader.DiscardBufferedData();
							streamReader.BaseStream.Position = 0;
							while ((line = streamReader.ReadLine()) != null)
							{
								if (line == LogPositionPlaceholder)
								{
									streamReader.DiscardBufferedData();
									result = streamReader.BaseStream.Position - PlaceHolderSize - 2;
									break;
								}
							}
						}
					}
				}
				finally
				{
					if (fileStream != null)
					{
						fileStream.Dispose();
					}
				}

				return result;
			}
		}
	}
}