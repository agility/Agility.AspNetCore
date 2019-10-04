//<script type="text/javascript">




/*
the agility context object is initialized in StatusPanelEmitter.cs
*/

/*
add the agility status panel to the page
*/

function initAgilityStatusPanel() {
    initJQueryElementsForAglityWebPreview();


    if (document.body == null || document.body.style == null || document.body.firstChild == null) {
        setTimeout(function () {
            initAgilityStatusPanel();
        }, 5);
        return;
    }



    //only show the preview panel if we are NOT in a frame
    try {
        if (window.parent.frames.length > 0 && window.parent != window) {
            return;
        }
    } catch (Error) { }

    //try to focus this window
    window.focus();



    //create the html for the status panel
    var html = "";


    if (agilityContextObj.isDevelopmentMode) {
        //development mode

        var largeSize = false;
        if (window.innerWidth > 630) largeSize = true;

        html += "<div class='AgilityDevMode'>";


        //RIGHT SIDE


        html += "<div class='AgilityDevModeFixedRight'";
        if (largeSize) {
            html += "<div class='AgilityDevModeFixedRightLarge'>";
        } else {
            html += "<div class='AgilityDevModeFixedRight'>";
        }


        //channel picker
        html += "<select id=\"agilitychannelPicker\" onchange=\"agilityChangeDigitalChannelID(this)\" class=\"ChannelDDL\" >";

        for (var i in agilityContextObj.channels) {
            html += "<option value=\"" + agilityContextObj.channels[i].ID
            if (agilityContextObj.channels[i].ID == agilityContextObj.channel) {
                html += "\" selected=\"selected"
            }
            html += "\">";
            html += agilityContextObj.channels[i].Name + "</option>";
        }
        html += "</select>";


        //language picker
        html += "<select class='LanguageDDL' style='" + ddlStyle + "' onchange=\"agility_PostBackChange('" + agilityContextObj.controlUniqueID + "', 'switchlanguage;'+ this.value)\">"

        if (agilityLanguages != null) {
            for (var i = 0; i < agilityLanguages.length; i++) {
                html += "<option value='" + agilityLanguages[i][1] + "' ";
                if (agilityLanguages[i][1] == agilityContextObj.languageCode) {
                    html += "selected";
                }
                html += ">" + agilityLanguages[i][0] + "</option>";
            }
        } else {
            html += "<option value='en-us'>English</option>";
        }
        html += "</select>";
        /*
        //little refresh button		
        html += "<a class='AgilityDevModeRefresh' href='javascript:;' onclick=\"agilityPreview_Refresh()\" title='Refresh Content'>";
        if (largeSize) {
            html += "<img class='AgilityLargeRefresh' src='ecms.ashx/Agility.Web.Res.Images.agilityBtnReloadWords.gif' alt='Refresh'/>";
        } else {
            html += "<img class='AgilitySmallRefresh' src='ecms.ashx/Agility.Web.Res.Images.agilityBtnRefresh.gif' alt='Refresh'/>";
        }
        html += "</a>";
		*/

        html += "</div>";
        /*
        //big refresh
        html += "<div class='AgilityDevModeRefreshLargeContainer'>";
        html += "<a class='AgilityDevModeRefresh' href='javascript:;' onclick=\"agilityPreview_Refresh()\" title='Refresh Content'>";					
        html += "<img class='AgilityLargeRefresh' src='ecms.ashx/Agility.Web.Res.Images.agilityBtnReloadWords.gif' alt='Refresh'/>";		
        html += "</a>";
        html += "</div>";
        */

        //LEFT SIDE
        //html += "<div class='AgilityDevModeFixedLeft'>";






        if (agilityContextObj.errorLink != null) {

            //err
            html += "<a href='" + agilityContextObj.errorLink + "' target='_blank'><img class='AgilityDevBarIconError' alt='' src='ecms.ashx/Agility.Web.Res.Images.agilityIconDevModeError.gif'/></a>";
            html += "<a class='AgilityDevBarError' href='" + agilityContextObj.errorLink + "' target='_blank' title='Click view error log.'>VIEW ERROR</div>";
        } else {
            html += "<img class='AgilityDevBarIcon' alt='' src='ecms.ashx/Agility.Web.Res.Images.agilityIconDevMode.gif'/>";
            html += "<div class='AgilityDevBarTitle'>DEVELOPMENT MODE</div>";
        }


        html += "</div>";

        html += "</div>";

    } else {
        //preview mode

        html += "<div class='Fix'>";
        html += "<div class='Locked'>";
        html += "<img class='Logo' src='ecms.ashx/Agility.Web.Res.Images.AgilityLogo.gif' alt=''>";



        if (agilityContextObj.errorLink != null) {
            //error
            html += "<div class='StatusPanel ErrorOccurred'>";
            html += "<div class='Icon'><img src='ecms.ashx/Agility.Web.Res.Images.iconError";
        } else if (agilityContextObj.currentMode == "Staging") {
            //staging
            html += "<div class='StatusPanel'>";
            html += "<div class='Icon'><img src='ecms.ashx/Agility.Web.Res.Images.";

            html += "iconInProgress";
        } else {
            //published		
            if (agilityContextObj.containsUnpublishedModules) {
                //has unpublished modules 
                html += "<div class='StatusPanel PublishWarning'>";
                html += "<div class='Icon'><img src='ecms.ashx/Agility.Web.Res.Images.iconPublishWarning";
            } else {
                html += "<div class='StatusPanel Published'>";
                html += "<div class='Icon'><img src='ecms.ashx/Agility.Web.Res.Images.iconPublished";
            }

        }
        html += ".gif' /></div>";
        html += "<div class='Left'>";





        html += "<div class='StateName'>";

        html += "Preview&nbsp;Mode&nbsp;-&nbsp;";

        if (agilityContextObj.errorLink != null) {
            //error
            html += "ERROR&nbsp;OCCURRED";
        } else if (agilityContextObj.isTemplatePreview) {
            //template preview
            html += "PAGE&nbsp;TEMPLATE";
        }
        else if (agilityContextObj.isDevelopmentMode) {
            //Show in Development Mode:
            html += "DEVELOPMENT";
        } else if (agilityContextObj.currentMode == "Staging") {
            //Show Status Panel:
            html += "STAGING";

        } else {
            html += "PUBLISHED";
        }

        html += "</div><br />";
        html += "<div class='StateMessage'>";

        if (agilityContextObj.errorLink != null) {
            //err
            html += "Click <a href='" + agilityContextObj.errorLink + "' target='_blank'>here</a> to view the error log.";
        } else if (agilityContextObj.isTemplatePreview) {
            html += "You are previewing the module zones on this Page Template";
        }
        else {
            if (agilityContextObj.containsUnpublishedModules) {
                html += "There are module definitions on this page that require publishing.";
            }
            else if (agilityContextObj.isDevelopmentMode) {

                html += "You are viewing this website in Development mode.";
            } else if (agilityContextObj.currentMode == "Staging") {

                html += "You are viewing this website in Staging mode.";

            } else {
                html += "You are viewing this website in Published mode.";
            }
        }


        html += "</div>";
        html += "</div>";

        html += "</div>";

        if (!agilityContextObj.isTemplatePreview) {

            //regular page preview
            html += "<div class='RightPanel'>";
            html += "<div class='Left'>";

            var ddlStyle = "visibility:hidden";
            if (agilityContextObj.currentMode == "Live" && agilityContextObj.errorLink == null && !agilityContextObj.containsUnpublishedModules) {
                ddlStyle = "";
            }

            //preview date dropdown 
            var dateStr = agilityContextObj.previewDateTime;
            if (agilityContextObj.previewDateTime == "") dateStr = "Preview Date";

            var datePickStyle = "";
            html += "<input id=\"agilityPreviewDatepicker\" style='" + ddlStyle + "' class='PreviewDateDDL' type=\"text\" title=\"Click to change the preview date.\" value=\"" + dateStr + "\"  />"

            html += "<div class='ddlSeparator'>&nbsp;</div>";

            ddlStyle = "visibility:hidden";
            if (agilityContextObj.errorLink == null && !agilityContextObj.containsUnpublishedModules) {
                ddlStyle = "";
            }

            html += "<select class='LanguageDDL'  onchange=\"agility_PostBackChange('" + agilityContextObj.controlUniqueID + "', 'switchlanguage;'+ this.value)\">"

            if (agilityLanguages != null) {
                for (var i = 0; i < agilityLanguages.length; i++) {
                    html += "<option value='" + agilityLanguages[i][1] + "' ";
                    if (agilityLanguages[i][1] == agilityContextObj.languageCode) {
                        html += "selected";
                    }
                    html += ">" + agilityLanguages[i][0] + "</option>";
                }
            } else {
                html += "<option value='en-us'>English</option>";
            }
            html += "</select>";
            html += "</div>"

            html += "<div class='ModeSel'>";

            var alertStr = "";
            if (agilityContextObj.containsUnpublishedModules) {
                alertStr = "alert('There are module definitions on this page that require publishing.'); ";
            }

            html += agilityPreview_injectCloseButton(agilityContextObj);

            if (agilityContextObj.currentMode == "Live") {
                //live
                html += "<span>&nbsp;Published&nbsp;</span>&nbsp;&nbsp;<span class='Sep'>|</span>&nbsp;&nbsp;<a href=\"javascript:void(0)\" onclick=\"agility_PostBackChange('" + agilityContextObj.controlUniqueID + "', 'switchToStaging')\">&nbsp;Staging&nbsp;</a>";
            } else if (agilityContextObj.isDevelopmentMode) {
                //development
                if (agilityContextObj.isPublished) {
                    //only allow switching to published if the page has been published
                    html += "<a href=\"javascript:void(0)\" onclick=\"alert('This website is currently locked in development mode.')\">&nbsp;Published&nbsp;</a>&nbsp;&nbsp;<span class='Sep'>|</span>&nbsp;&nbsp;<span>&nbsp;Staging&nbsp;</span>";
                } else {

                    html += "<span style='visibility:hidden'>&nbsp;Published&nbsp;</span>&nbsp;&nbsp;<span  style='visibility:hidden' class='Sep'>|</span>&nbsp;&nbsp;<span>&nbsp;Staging&nbsp;</span>";
                }
            } else {
                //staging

                if (agilityContextObj.isPublished) {
                    //only allow switching to published if the page has been published

                    html += "<a href=\"javascript:void(0)\" onclick=\"" + alertStr + "agility_PostBackChange('" + agilityContextObj.controlUniqueID + "', 'switchToLive')\">&nbsp;Published&nbsp;</a>&nbsp;&nbsp;<span class='Sep'>|</span>&nbsp;&nbsp;<span>&nbsp;Staging&nbsp;</span>";
                } else {
                    html += "<span style='visibility:hidden'>&nbsp;Published&nbsp;</span>&nbsp;&nbsp;<span  style='visibility:hidden' class='Sep'>|</span>&nbsp;&nbsp;<span>&nbsp;Staging&nbsp;</span>";
                }
            }


            //show the page template preview
            if (agilityContextObj.pageTemplatePath != "") {
                var templateUrl = agilityContextObj.pageTemplatePath + "?agilityPageDefinitionID=" + agilityContextObj.pageTemplateID;

                if (agilityContextObj.currentMode == "Staging") {

                    templateUrl += "&currentState=Staging";

                } else {
                    templateUrl += "&currentState=Published";
                }
                templateUrl += "&currentUrl=" + escape(location.href);

                html += "&nbsp;&nbsp;<span class='Sep'>|</span>&nbsp;&nbsp;<a href=\"" + templateUrl + "\">&nbsp;Template&nbsp;</a>&nbsp;";
            }



            html += "</div>";




            html += "</div>";
        } else {
            //page template preview

            html += "<div class='ModeSel'>";

            html += agilityPreview_injectCloseButton(agilityContextObj);

            var modeUrl = location.href;

            if (modeUrl.toLowerCase().indexOf("&currenturl=") > 0) {
                modeUrl = modeUrl.substring(modeUrl.toLowerCase().indexOf("&currenturl=") + 12);
                if (modeUrl.indexOf("&") > 0) {
                    modeUrl = modeUrl.substring(0, modeUrl.indexOf("&"));
                }
            } else {
                modeUrl = null;
            }



            //show the link to get back to staging/publish preview	
            if (modeUrl != null && modeUrl != "") {

                html += "<div style='float:left; width:134px'>&nbsp;&nbsp;</div>";
                if (location.href.toLowerCase().indexOf("currentstate=staging") > 0) {
                    html += "<a href=\"#\" style=\"visibility:hidden\">&nbsp;Published&nbsp;</a>";
                    html += "&nbsp;&nbsp;<span class='Sep' style=\"visibility:hidden\">|</span>&nbsp;&nbsp;";
                    html += "<a href=\"" + unescape(modeUrl) + "\" >&nbsp;Staging&nbsp;</a>";
                }
                else {
                    html += "<a href=\"" + unescape(modeUrl) + "\" >&nbsp;Published&nbsp;</a>";
                    html += "&nbsp;&nbsp;<span class='Sep' style=\"visibility:hidden\">|</span>&nbsp;&nbsp;";
                    html += "<a href=\"#\" style=\"visibility:hidden\">&nbsp;Staging&nbsp;</a>";

                }

                html += "&nbsp;&nbsp;<span class='Sep'>|</span>&nbsp;&nbsp;";

                //		    html += "&nbsp;&nbsp;<span class='Sep'>|</span>&nbsp;&nbsp;<a href=\"" + unescape(modeUrl) + "\" >&nbsp;" + modeFromUrl + "&nbsp;</a>";
            }


            html += "<span>&nbsp;Template&nbsp;</span>";



        }

        html += "<div style='clear:both'></div>"
        html += "</div>"



        //the div that contains the date and time picker...
        html += "<div id='pnlAgilityDateTimePicker'><div id='pnlAgilityDatePicker'></div><div id='pnlAgilityTimerPicker'><div  id='pnlAgilityTimeDropDown' class='ui-widget ui-widget-content ui-helper-clearfix ui-corner-all'>"
        html += "<select id='ddlAgilityTimerPicker'>";

        for (var i = 0; i < 12; i++) {

            html += "<option>";
            if (i == 0) {
                html += "12:00";
            } else {
                html += i + ":00";
            }
            html += " AM</option>";
        }

        for (var i = 0; i < 12; i++) {

            html += "<option>";
            if (i == 0) {
                html += "12:00";
            } else {
                html += i + ":00";
            }
            html += " PM</option>";
        }

        html += "</select>";
        html += "</div>"
        html += "<div id='pnlAgilityTimePickerButtons'><button id='btnAgilityDateTimeOK'>OK</button>&nbsp;<button id='btnAgilityDateTimeCancel'>Cancel</button></div>";
        html += "</div></div>";



        //close off the panel
        html += "</div>";

        //add the html to the page


    }

    var div = document.createElement("div");

    if (agilityContextObj.isDevelopmentMode) {
        div.className = "AgilityDevBar";
    } else {
        div.className = "AgilityStatusBar";
    }

    div.id = "pnlAgilityStatusBar";
    div.innerHTML = html;

    div.style.marginBottom = document.body.style.marginTop;
    document.body.style.marginTop = '0px';

    //figure out the proper left margin for the bar
    var ml = "10px";
    if (document.body.currentStyle) {
        ml = document.body.currentStyle.marginLeft;
    } else {
        ml = document.defaultView.getComputedStyle(document.body, null).getPropertyValue("margin-left");
    }

    //handle auto margin
    if (ml != "" && (!ml.indexOf("-") == 0)) {
        try {
            div.style.marginLeft = "-" + ml;
        } catch (Error) { }
    }

    //try to put this into a special div...
    var elemToInsert = document.getElementById("pnlAgilityStatusBarContainer");
    if (elemToInsert != null) {
        elemToInsert.appendChild(div);
    } else {
        document.body.insertBefore(div, document.body.firstChild);
    }


}


var Agility_jQueryScriptOutputted = false;

///Initialize the jQuery elements of the preview bar
function initJQueryElementsForAglityWebPreview() {
    if (typeof (jQuery) == 'undefined') {
        if (!Agility_jQueryScriptOutputted) {
            Agility_jQueryScriptOutputted = true;
            //document.write("<scr" + "ipt type=\"text/javascript\" src=\"ecms.ashx/Agility.Web.Scripts.jquery-1.5.2.min.js\"></scr" + "ipt>");
            //document.write("<scr" + "ipt type=\"text/javascript\" src=\"ecms.ashx/Agility.Web.Scripts.jquery-ui-1.8.custom.min.js\"></scr" + "ipt>");
            Agility_jQueryScriptOutputted = true;
        }
        setTimeout(function () { initJQueryElementsForAglityWebPreview(); }, 50);

    } else {

        //add the jquery.ui for agility preview (only if we haven't already done so
        if (!Agility_jQueryScriptOutputted) {

            //document.write("<scr" + "ipt type=\"text/javascript\" src=\"ecms.ashx/Agility.Web.Scripts.jquery-ui-1.8.custom.min.js\"></scr" + "ipt>");
        }

        var _startup = function () {
            if (jQuery("#pnlAgilityDatePicker").datepicker == undefined || jQuery("#pnlAgilityDatePicker").datepicker == null) {
                setTimeout(_startup, 50);
                return;
            }

            jQuery("#pnlAgilityDatePicker").datepicker({ duration: "", minDate: 0, maxDate: '+1Y', dateFormat: 'yy-mm-dd' });

            jQuery("#agilityPreviewDatepicker").click(function () {
                var pos = jQuery("#agilityPreviewDatepicker").position();
                var h = jQuery("#agilityPreviewDatepicker").outerHeight();
                var left = pos.left + 9;
                jQuery("#pnlAgilityDateTimePicker").css("left", left + "px");
                jQuery("#pnlAgilityDateTimePicker").css("top", (pos.top + h) + "px");
                jQuery("#pnlAgilityDateTimePicker").show();
            });

            jQuery("#agilityPreviewDatepicker").change(function () {
                agilityChangePreviewDate(jQuery(this).val());
            });


            jQuery("#btnAgilityDateTimeOK").click(function () {
                var dt = new Date();

                dt = jQuery("#pnlAgilityDatePicker").datepicker('getDate');

                var timeValue = jQuery("#ddlAgilityTimerPicker").val();
                var dtVal = dt.getFullYear() + "-" + (dt.getMonth() + 1) + "-" + dt.getDate() + " " + timeValue;

                jQuery("#agilityPreviewDatepicker").val(dtVal);
                agilityChangePreviewDate(dtVal);
                jQuery("#pnlAgilityDateTimePicker").hide();


                return false;
            });


            jQuery("#btnAgilityDateTimeCancel").click(function () {
                jQuery("#pnlAgilityDateTimePicker").hide();
                return false;
            });
        }

        //load the status panel on document.ready
        jQuery(function () {

            _startup();
        });
    }
}




function agilityPreview_injectCloseButton(agilityContextObj) {

    var html = "<div class='divTopRow'>";

    //the mail link
    html += "<a href=\"" + agilityContextObj.previewURL + "\">&nbsp;Email Page&nbsp;</a>&nbsp;&nbsp;<span class=\"Sep\">|</span>&nbsp;&nbsp;";

    if (agilityContextObj.pageID > 0) {
        var editUrl = "http://contentmanager.agilitycms.com/PagesAndContent.aspx?pic=" + agilityContextObj.pageID + "&lang=" + escape(agilityContextObj.languageCode) + "&w=" + escape(agilityContextObj.websiteName);
        html += "<a href=\"" + editUrl + "\" target=\"AgilityContentManager\">&nbsp;Edit Page&nbsp;</a>&nbsp;&nbsp;<span class=\"Sep\">|</span>&nbsp;&nbsp;";
    }



    if (agilityContextObj.isDevelopmentMode == false) {
        //the close button
        html += "<a href='javascript:;' title='Click to exit preview mode.' onclick=\"agilityPreview_endPreview(); return false;\" >&nbsp;Exit Preview&nbsp;</a>";
    } else {
        //the refresh button
        html += "<a href='javascript:;' onclick=\"agilityPreview_Refresh()\">&nbsp;Refresh&nbsp;</a>";
    }

    html += "</div>";

    return html;
}

function agilityPreview_getDateString(d) {
    return d.getFullYear() + "-" + (d.getMonth() + 1) + "-" + d.getDate();
}

function agilityPreview_Refresh() {

    agilityPreview_createCookie(agilityContextObj.websiteName + "_Refresh", "1", null, agilityContextObj.cookieDomain);
    location.reload();
}

function agilityPreview_endPreview() {
    if (confirm("Do you wish to exit your preview session?")) {
    	
    	agilityPreview_eraseCookie(websiteName + "_IsPreview", agilityContextObj.cookieDomain);
        agilityPreview_eraseCookie(websiteName + "_IsPreview", "");
        agilityPreview_eraseCookie(websiteName + "_Mode", agilityContextObj.cookieDomain);
        agilityPreview_eraseCookie(websiteName + "_Mode", "");
        agilityPreview_eraseCookie(websiteName + "_AgilityChannel", "");

        window.location.reload();

    }
}

function agilityChangeDigitalChannelID(select) {

    var url = location.href;
    var newUrl = location.pathname;

    var index = url.indexOf("?");
    if (index > 1 && index < url.length - 2) {
        //pull apart the query string and put it back together, taking OUT AgilityChannel
        if (url.indexOf("#") != -1) {
            url = url.substring(0, url.indexOf("#"));
        }

        var qstr = url.substring(index + 1, url.length);

        var ary1 = qstr.split("&");

        for (var i in ary1) {
            var ary2 = ary1[i].split("=");
            if (ary2.length == 2) {
                if (ary2[0].toLowerCase().indexOf("agilitychannel") != -1) continue;

                if (newUrl.indexOf("?") == -1) {
                    newUrl += "?";
                } else {
                    newUrl += "&";
                }

                newUrl += ary2[0] + "=" + ary2[1];

            }
        }

    }

    //add the new query string
    if (newUrl.indexOf("?") == -1) {
        newUrl += "?";
    } else {
        newUrl += "&";
    }
    newUrl += "AgilityChannel=" + select.value;

    location.href = newUrl;

}

function agilityChangePreviewDate(dateValue) {

    agility_PostBackChange(agilityContextObj.controlUniqueID, 'switchpreviewdate;' + dateValue)
}

function agility_PostBackChange(ctrlID, arg) {



    var form = jQuery("<form id='" + ctrlID + "' method='POST' action='" + location.href + "'><input type='hidden' name='agilitypostback' value=\"" + arg + "\" /></form>");
    form.appendTo(document.body)

    form.submit();
}


function agilityPreview_createCookie(name, value, days, cookieDomain) {
    var expires = "";
    var domain = "";
    if (days != undefined && !isNaN(days)) {
        var date = new Date();
        date.setTime(date.getTime() + (days * 24 * 60 * 60 * 1000));
        var expires = "; expires=" + date.toGMTString();
    }

    if (cookieDomain != undefined && cookieDomain != "") {
        domain = "; domain=" + cookieDomain;
    }

    var cookie = name + "=" + escape(value) + expires + "; path=/" + domain;

    document.cookie = cookie;
}

function agilityPreview_readCookie(name) {
    var nameEQ = name + "=";
    var ca = document.cookie.split(';');
    for (var i = 0; i < ca.length; i++) {
        var c = ca[i];
        while (c.charAt(0) == ' ') c = c.substring(1, c.length);
        if (c.indexOf(nameEQ) == 0) return unescape(c.substring(nameEQ.length, c.length));
    }
    return null;
}

function agilityPreview_eraseCookie(name, cookieDomain) {

    agilityPreview_createCookie(name, "", -1, cookieDomain);
}

