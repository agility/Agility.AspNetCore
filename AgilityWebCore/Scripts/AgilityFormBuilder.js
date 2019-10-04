/// <reference path="http://localhost:51370/scripts/jquery-1.5.1-vsdoc.js" />
/// <reference path="http://localhost:51370/scripts/agility.ugc.api.js" />
Agility.RegisterNamespace("Agility.CMS.API.FormBuilder");

var SWFUpload = {};

SWFUpload.QUEUE_ERROR = {
	QUEUE_LIMIT_EXCEEDED: -100,
	FILE_EXCEEDS_SIZE_LIMIT: -110,
	ZERO_BYTE_FILE: -120,
	INVALID_FILETYPE: -130
};
SWFUpload.UPLOAD_ERROR = {
	HTTP_ERROR: -200,
	MISSING_UPLOAD_URL: -210,
	IO_ERROR: -220,
	SECURITY_ERROR: -230,
	UPLOAD_LIMIT_EXCEEDED: -240,
	UPLOAD_FAILED: -250,
	SPECIFIED_FILE_ID_NOT_FOUND: -260,
	FILE_VALIDATION_FAILED: -270,
	FILE_CANCELLED: -280,
	UPLOAD_STOPPED: -290
};
SWFUpload.FILE_STATUS = {
	QUEUED: -1,
	IN_PROGRESS: -2,
	ERROR: -3,
	COMPLETE: -4,
	CANCELLED: -5
};


(function (FB) {

	$(function () {

		$(".AgilityFormBuilder input[type='submit']").click(formSubmission);

		$(".AgilityFormBuilder").each(function () {
			var formElem = $(this);
			var recordTypeName = formElem.attr("recordTypeName");

			//load the recordType into memory so we have it...
			Agility.UGC.API.GetRecordTypeByName(recordTypeName, function (data) {
				if (data.ResponseType == Agility.UGC.API.ResponseType.OK) {

					formElem.data("recordType", data.ResponseData);

					var recordType = data.ResponseData;

					$(".AgilityFile", formElem).each(function () {
						initFileFields($(this), recordType);
					});

					//keep track of any file field types...
					var fileFieldNames = [];
			
					for (var i in recordType.FieldTypes) {
						var fieldType = recordType.FieldTypes[i];
						if (fieldType.DataType == Agility.UGC.API.DataType.File) {
							fileFieldNames.push(fieldType.Name);
						}

						//check out if any of the fields in the recordtype are in querystring.
						var val = Agility.QueryString(fieldType.Name);
						if (val != null && val != "") {
							$("[name='" + fieldType.Name + "']", formElem).val(val);
						}


					}
					
					formElem.data("fileFieldNames", fileFieldNames);

					
				


				}
			});

			


		});
	
	});


	getUGCSettings = function (callback) {
		Agility.UGC.API.GetSettings(function (data) {
			callback(data.ResponseData);
		});
	}

	initFileFields = function (div, recordType) {
		
		var formElem = $(this).parents("div.AgilityFormBuilder");
		
		var fieldName = div.attr("fieldName");
		var inputID = div.attr("inputID");
		var input = $("#" + inputID);

		var fieldType = null;
		for (var i in recordType.FieldTypes) {
			if (recordType.FieldTypes[i].Name == fieldName) {
				fieldType = recordType.FieldTypes[i];
			}
		}

		//create the elements we need - filename display, upload button, remove button, progress
		
		var pnlID = inputID + "_UploadPanel";
		var html = "<div class='AgilityUploadedFile'></div><div class='AgilityUploadedFileRemove'></div>";
		html += "<div class='AgilityUploadPanel' id='" + pnlID + "'></div>";
		html += "<div class='AgilityUploadProgressMessage'></div>";
		html += "<div class='AgilityUploadProgressContainer'><div class='AgilityUploadProgress'></div></div>";
		div.append(html);

		$(".AgilityUploadedFileRemove", div).click(function () {
			//remove the file...
			$(".AgilityUploadedFile", div).html("");
			$(".AgilityUploadedFileRemove", div).hide();
			$(".AgilityUploadPanel", div).show();
			input.val("");
		});

		//default the previously uploaded file...
		if (input.val() != "") {
			var filenameStr = input.val();
			if (filenameStr.indexOf("/") > 0) {
				filenameStr = filenameStr.substring(filenameStr.lastIndexOf("/") + 1);
			}

			$(".AgilityUploadedFile", div).html(filenameStr);
			$(".AgilityUploadedFileRemove", div).css("display", "inline-block");

			//TODO: preview image?
		} 
		
		Agility.UGC.API.GetAmazonS3Form({
			fieldname: fieldName,
			inputID: inputID,
			fieldType: fieldType,
			fieldPanel: $("#" + pnlID),
			swfUploadUrl: null,

			beforeUpload: function (fileName, mime, filesize, uploader) {

				var errorContainer = $(".AgilityUploadProgressMessage", div);
				errorContainer.html("");
				errorContainer.hide();
				$(".AgilityUploadProgressContainer", div).hide();


				//check max file size...
				if (filesize > 0 && fieldType.MaxLength > 0) {
					if ((filesize / 1024) > fieldType.MaxLength) {
						var maxfileSizeKB = fieldType.MaxLength;
						errorContainer.html("The file must be less than " + maxfileSizeKB + "KB");
						errorContainer.show();
						if (uploader != null) uploader.cancelUpload(null, false);
						return false;
					}
				}

				$(".AgilityUploadProgressContainer", div).show();
				
			},
			uploadComplete: function (key) {

				input.val(key);
				
				var filenameStr = key;
				if (filenameStr.indexOf("/") > 0) {
					filenameStr = filenameStr.substring(filenameStr.lastIndexOf("/") + 1);
				}

				$(".AgilityUploadedFile", div).html(filenameStr);
				$(".AgilityUploadedFileRemove", div).css("display", "inline-block");

				//TODO: show a thumbnail if it's an image?
				$(".AgilityUploadProgressContainer", div).hide();
				//hide the upload panel..
				$(".AgilityUploadPanel", div).hide();

			},
			uploadError: function (file, errorCode, message) {

				var errorMessage = message
				if (errorMessage == undefined || errorMessage == null || errorMessage == "") {
					errorMessage = "An error occurred while uploading the file.";
				}

				$(".AgilityUploadProgressContainer", div).hide();
				$(".AgilityUploadProgressMessage", div).html(errorMessage).show();
			},
			uploadProgress: function (file, bytesLoaded) {
				$(".AgilityUploadProgressContainer", div).show();
				//var percent = Math.ceil((bytesLoaded / file.size) * 100);
				//$(".AgilityUploadProgress", div).css("width", percent + "%");
			}
		});
		
	}

	outputError = function (formElem, referenceName, contentID, message) {

		getFieldValue(referenceName, contentID, "ErrorTemplate", function (data) {

			var html = data;
			if (message != null && message != "") {
				message = message.replace(/\n/g, "<br/>");
				html += "<p>" + message + "</p>";
			}
			$(".AgilityFormValidationArea", formElem).html(html);

		});

	}

	getFieldValue = function (referenceName, contentID, fieldName, callback) {

		var url = "ecms.ashx/?referenceName=" + referenceName + "&contentID=" + contentID + "&fieldName=" + fieldName;

		$.get(url, function (data) {
			callback(data);
		});
	}

	showProgress = function (formElem) {

		var submitElem = $("input[type='submit']", formElem);

		var submitHtml = submitElem.val();
		submitElem.data("submitHtml", submitHtml);
		submitElem.val("        ");
		submitElem.attr("disabled", true).addClass("PendingSubmit");
	}

	hideProgress = function (formElem) {

		var submitElem = $("input[type='submit']", formElem);
		submitElem.val(submitElem.data("submitHtml"));
		submitElem.attr("disabled", false).removeClass("PendingSubmit");
	}
	
	formSubmission = function (e) {
		e.preventDefault();
		var formElem = $(this).parents("div.AgilityFormBuilder");
		
		var recordTypeName = formElem.attr("recordTypeName");
		if (recordTypeName == null) {
			vldElem.html("Configuration Error: This form has not been properly configured.");
			hideProgress(formElem);
			return;
		}

		var vldElem = $(".AgilityFormValidationArea", formElem);
		vldElem.html("");

		showProgress(formElem);

		//get the record type

		var recordType = formElem.data("recordType");
		if (recordType != null) {
			formSubmission2(formElem, recordType);
		} else {

			Agility.UGC.API.GetRecordTypeByName(recordTypeName, function (data) {
				if (data.ResponseType == Agility.UGC.API.ResponseType.OK) {

					formElem.data("recordType", data.ResponseData);
					formSubmission2(formElem, data.ResponseData);
				} else {
					vldElem.html("Configuration Error: The record type " + recordTypeName + " could not be accessed.");
					hideProgress(formElem);
				}
			});
		}
		return false;
	}

	formSubmission2 = function (formElem, recordType) {
		
		var containerElem = formElem.parents("div.AgilityFormBuilderContainer");

		var referenceName = containerElem.attr("referenceName");
		var contentID = containerElem.attr("contentID");
		var responseType = containerElem.attr("responseType");

		var vldElem = $(".AgilityFormValidationArea", formElem);

		var recordTypeName = formElem.attr("recordTypeName");
		
		//reset the validation classes...
		$(".ValidationError", formElem).removeClass("ValidationError");

		
		//build the record
		var record = {
			RecordTypeName: recordTypeName
		};

		var fieldNames = [];

		//validate...
		var lstFieldErrors = [];

		var emailField = null;

		for (var i = 0; i < recordType.FieldTypes.length; i++) {
			var fieldType = recordType.FieldTypes[i];
			var fieldName = fieldType.Name;

			var fieldElem = $("[name='" + fieldType.Name + "']", formElem);

			var errorCount = lstFieldErrors.length;

			if (fieldType.DataType == Agility.UGC.API.DataType.Email && emailField == null) {
				emailField = fieldName;
			}

			fieldNames.push(fieldName);
			var fieldValue = null;

			if (fieldElem.attr("type") == "checkbox") {
				fieldValue = fieldElem.attr("checked");
				if (fieldValue == "checked") {
					fieldValue = true;
				}

				if (fieldType.AllowNull == false && fieldValue != true) {
					fieldValue = null;
				}

			} else {
				fieldValue = fieldElem.val();
			}
	
			var fieldLabel = fieldType.Label;

			if (fieldType.AllowNull == false) {
				//req field
				if (fieldValue == undefined || fieldValue == null || new String(fieldValue).length == 0) {
					lstFieldErrors.push("The " + fieldType.Label + " field is required.");
					fieldElem.addClass("ValidationError");
				}
			}

			if (fieldValue == undefined || fieldValue == null || fieldValue == "") {
				//skip null vals if they are allowed...
				continue;
			}


			switch (fieldType.DataType) {

				case Agility.UGC.API.DataType.Boolean:
					if (new String(fieldValue).toLowerCase() != "true"
						&& new String(fieldValue).toLowerCase() != "false") {
						lstFieldErrors[lstFieldErrors.length] = "Could not convert value " + fieldValue + " from field " + fieldType.Label + " to a boolean.";
					}
					break;
				case Agility.UGC.API.DataType.DateTime:

					var dt = new Date(fieldValue);
					if (dt == null) {
						lstFieldErrors[lstFieldErrors.length] = "Could not convert value " + fieldValue + " from field " + fieldType.Label + " to a date/time.";
					}
					break;
				case Agility.UGC.API.DataType.Double:

					if (isNaN(parseFloat(fieldValue))) {
						lstFieldErrors[lstFieldErrors.length] = "Could not convert value " + fieldValue + " from field " + fieldType.Label + " to a number.";
					}
					break;
				case Agility.UGC.API.DataType.Int:
					if (isNaN(parseInt(fieldValue))) {
						lstFieldErrors[lstFieldErrors.length] = "Could not convert value " + fieldValue + " from field " + fieldType.Label + " to a number.";
					}
					break;
				case Agility.UGC.API.DataType.String:
					//enforce max length...				
					if (fieldType.MaxLength == 0 && fieldValue.Length > 400) {
						lstFieldErrors[lstFieldErrors.length] = "The value from field " + fieldType.Label + " must be 400 characters or less.";

					}
					else if (fieldType.MaxLength > 0 && fieldValue.Length > fieldType.MaxLength) {
						lstFieldErrors[lstFieldErrors.length] = "The value from field " + fieldType.Label + " must be " + fieldType.MaxLength + " characters or less.";
					}

					var o1 = _enforceRegex(fieldType, fieldValue, fieldLabel);
					if (o1 != null) {
						lstFieldErrors[lstFieldErrors.length] = o1;
					}

					break;

				default:
					//textblob - enforce max length

					if (fieldType.MaxLength > 0 && fieldValue.Length > fieldType.MaxLength) {
						lstFieldErrors[lstFieldErrors.length] = "The value from field " + fieldType.Label + " must be " + fieldType.MaxLength + " characters or less.";
					}

					var o2 = _enforceRegex(fieldType, fieldValue, fieldLabel);
					if (o2 != null) {
						lstFieldErrors[lstFieldErrors.length] = o2;
					}
					break;
			}
			
				
			if (fieldName != null && fieldName != ""
				&& fieldValue != null && fieldValue != "") {
				record[fieldName] = fieldValue;
			}

			if (lstFieldErrors.length > errorCount && (! fieldElem.hasClass("ValidationError"))) {
				fieldElem.addClass("ValidationError");
			}

		}
		//end field loop...

		//check for validation errors
		if (lstFieldErrors.length > 0) {
			var msg = "<ul><li>" + lstFieldErrors.join("</li><li>") + "</li></ul>";

			hideProgress(formElem);
			outputError(formElem, referenceName, contentID, msg);
			return;
		}


		//check for captcha
		handleCaptcha(formElem, function (captchaError) {

			if (captchaError != null) lstFieldErrors.push(captchaError);

			//check for validation errors
			if (lstFieldErrors.length > 0) {
				var msg = "<ul><li>" + lstFieldErrors.join("</li><li>") + "</li></ul>";

				hideProgress(formElem);
				outputError(formElem, referenceName, contentID, msg);
				return;
			}


			var afterSave = function (recordID) {

				//do any post processing
				var postProcessMethod = function (postProcess) {
					if (responseType != "Thanks") {
						//redirect
						
							location.href = postProcess;
						
					} else {
						//show the thank-you template
						formElem.html(postProcess);
					}
				};

				var postProcessUrl = "ecms.ashx/?referenceName=" + referenceName + "&contentID=" + contentID
				postProcessUrl += "&recordID=" + recordID + "&action=FormBuilderPostProcessing";
				if (emailField != null) {
					postProcessUrl += "&emailField=" + emailField;
				}
				//any file fields will need to be updated with the full URL...				
				getUGCSettings(function (settings) {

					var fileFieldNames = formElem.data("fileFieldNames");
					if (fileFieldNames != null) {
						var baseUrl = settings.AmazonS3BaseUrl;
						if (baseUrl.lastIndexOf("/") != baseUrl.length - 1) {
							baseUrl += "/";
						}

						for (var i in fileFieldNames) {
							var filepath = record[fileFieldNames[i]];
							if (filepath == undefined) continue;

							record[fileFieldNames[i]] = baseUrl + filepath;

						}
					}


					$.ajax(postProcessUrl, {
						type: "POST",
						data: record,
						success: postProcessMethod,
						error: function () {
							outputError(formElem, referenceName, contentID, "");
						}
					});
				});
			}

			if (containerElem.attr("submitIntoUGC") != "true") {
				afterSave(-1);
			} else {

				//save the record if we need to
				Agility.UGC.API.SaveRecord(record, function (data) {


					if (data.ResponseType != Agility.UGC.API.ResponseType.OK) {

						hideProgress(formElem);

						//show the error template and the error message...
						var msg = data.Message;
						if (msg == undefined || msg == null || msg == "") msg = "An error occurred.  Please try again.";

						outputError(formElem, referenceName, contentID, msg);
					} else {
						afterSave(data.ResponseData);
					}
				});
			}
		});
	};



	handleCaptcha = function (formElem, callback) {

		var containerElem = formElem.parents("div.AgilityFormBuilderContainer");
		var referenceName = containerElem.attr("referenceName");
		var contentID = containerElem.attr("contentID");

		if (containerElem.attr("captcha") == "true") {
			//has captcha
			var recaptcha_challenge_field = $("input[name='recaptcha_challenge_field']", formElem);
			var recaptcha_response_field = $("input[name='recaptcha_response_field']", formElem);

			if (recaptcha_response_field.val() == "") {
				callback("Invalid CAPTCHA.");
				return;
			}

			var url = "ecms.ashx?action=FormBuilderPreProcessing&referenceName="+ referenceName;
			url += "&contentID=" + contentID;
			url += "&challenge=" + recaptcha_challenge_field.val();
			url += "&response=" + recaptcha_response_field.val().replace(" ", "%20");
			
			

			$.get(url, function (data) {
				if (data == "true") {
					callback(null);
				} else {
					//show a captcha error and reset it
					recaptcha_challenge_field.val("");
					try {
						Recaptcha.reload();
					} catch (error) { }

					callback("Invalid CAPTCHA.");
					return;
				}
			});

		} else {
			//no captcha
			callback(null);
		}

	}

	_enforceRegex = function (fieldType, fieldValue, fieldLabel) {
		if (fieldValue == undefined || fieldValue == null || fieldValue == "" || typeof fieldValue != "string") return null;

		//enfore regex
		if (fieldType.ValidationRegEx != null && fieldType.ValidationRegEx != "" && fieldType.ValidationRegEx != "Add comma separated file extensions. Eg: .pdf, .gif") {
			try {
				var rEx = new RegExp(fieldType.ValidationRegEx);
				rEx.ignoreCase = true;

				if (fieldValue.search(rEx) == -1) {


					if (fieldType.ValidationMessage != null && fieldType.ValidationMessage != "" && fieldType.ValidationMessage != "Add the file type validation message.") {
						return  fieldType.ValidationMessage ;
					}
					else {
						return "The value from field " + fieldType.Label + " did not match the validation expression requirements.";
					}
				}
			} catch (Error) {
				//ignore regex errors...
			}
		}
	}

})(Agility.CMS.API.FormBuilder);