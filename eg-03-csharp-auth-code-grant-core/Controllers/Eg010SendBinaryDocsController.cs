﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using DocuSign.eSign.Model;
using eg_03_csharp_auth_code_grant_core.Models;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;

namespace eg_03_csharp_auth_code_grant_core.Controllers
{
    [Route("eg010")]
    public class Eg010SendBinaryDocsController : EgController
    {
        private const string V = "";        

        public Eg010SendBinaryDocsController(DSConfiguration config, IRequestItemsService requestItemsService)
            : base(config, requestItemsService)
        {
            ViewBag.title = "Send envelope with multipart mime";
        }

        public override string EgName => "eg010";

        [HttpPost]
        public IActionResult Create(string signerEmail, string signerName, string ccEmail, string ccName)
        {
            bool tokenOk = CheckToken(3);
            if (!tokenOk)
            {
                // We could store the parameters of the requested operation 
                // so it could be restarted automatically.
                // But since it should be rare to have a token issue here,
                // we'll make the user re-enter the form data after 
                // authentication.
                RequestItemsService.EgName = EgName;
                return Redirect("/ds/mustAuthenticate");
            }
            // Step 1. Make the envelope JSON request body
            dynamic envelope = MakeEnvelope(signerEmail, signerName, ccEmail, ccName);

            // Step 2. Gather documents and their headeres
            // Read files from a local directory
            // The reads could raise an exception if the file is not available! 
            dynamic doc1 = envelope.documents[0];
            dynamic doc2 = envelope.documents[1];
            dynamic doc3 = envelope.documents[2];

            dynamic documents = new[] {
                new {
                    mime = "text/html",
                    filename = (string) doc1.name,
                    documentId = (string) doc1.documentId,
                    bytes = Encoding.ASCII.GetBytes(document1(signerEmail, signerName, ccEmail, ccName))
                },
                new {
                    mime = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                    filename = (string) doc2.name,
                    documentId = (string) doc2.documentId,
                    bytes = System.IO.File.ReadAllBytes(Config.docDocx)
                },
                new {
                    mime = "application/pdf",
                    filename = (string) doc3.name,
                    documentId = (string) doc3.documentId,
                    bytes = System.IO.File.ReadAllBytes(Config.docPdf)
                }
            };

            // Step 3. Create the multipart body
            byte[] CRLF = Encoding.ASCII.GetBytes("\r\n");
            byte[] boundary = Encoding.ASCII.GetBytes("multipartboundary_multipartboundary");
            byte[] hyphens = Encoding.ASCII.GetBytes("--");

            string uri = RequestItemsService.Session.BasePath
                    + "/restapi/v2/accounts/" + RequestItemsService.Session.AccountId + "/envelopes";
            HttpWebRequest request = WebRequest.CreateHttp(uri);

            request.Method = "POST";
            request.Accept = "application/json";
            request.ContentType = "multipart/form-data; boundary=" + Encoding.ASCII.GetString(boundary);
            request.Headers.Add("Authorization", "Bearer " + RequestItemsService.User.AccessToken);

            using (var buffer = new BinaryWriter(request.GetRequestStream(), Encoding.ASCII))
            {
                buffer.Write(hyphens);
                buffer.Write(boundary);
                buffer.Write(CRLF);
                buffer.Write(Encoding.ASCII.GetBytes("Content-Type: application/json"));
                buffer.Write(CRLF);
                buffer.Write(Encoding.ASCII.GetBytes("Content-Disposition: form-data"));
                buffer.Write(CRLF);
                buffer.Write(CRLF);
                
                var json = JsonConvert.SerializeObject(envelope, Formatting.Indented);
                buffer.Write(Encoding.ASCII.GetBytes(json));
                // Loop to add the documents.
                // See section Multipart Form Requests on page https://developers.docusign.com/esign-rest-api/guides/requests-and-responses
                foreach (var d in documents)
                {
                    buffer.Write(CRLF);
                    buffer.Write(hyphens);
                    buffer.Write(boundary);
                    buffer.Write(CRLF);
                    buffer.Write(Encoding.ASCII.GetBytes("Content-Type:" + d.mime));
                    buffer.Write(CRLF);
                    buffer.Write(Encoding.ASCII.GetBytes("Content-Disposition: file; filename=\"" + d.filename + ";documentid=" + d.documentId));
                    buffer.Write(CRLF);
                    buffer.Write(CRLF);
                    buffer.Write(d.bytes);
                }

                // Add closing boundary
                buffer.Write(CRLF);
                buffer.Write(hyphens);
                buffer.Write(boundary);
                buffer.Write(hyphens);
                buffer.Write(CRLF);
                buffer.Flush();
            }

            WebResponse response = null;
            try
            {
                response = request.GetResponse();
            }
            catch (WebException ex)
            {
                response = ex.Response;
                ViewBag.err = ex;
            }

            var res = "";

            using (var stream = response.GetResponseStream())
            {
                using (var reader = new StreamReader(stream))
                {
                    res = reader.ReadToEnd();
                }
            }

            HttpStatusCode code = ((HttpWebResponse)response).StatusCode;
            dynamic obj = JsonConvert.DeserializeObject(res);
            if (code >= HttpStatusCode.OK && code < HttpStatusCode.MultipleChoices)
            {                
                RequestItemsService.EnvelopeId = obj.envelopeId;
                ViewBag.h1 = "Envelope sent";
                ViewBag.message = "The envelope has been created and sent!<br/>Envelope ID " + obj.envelopeId + ".";
                return View("example_done");
            }
            else
            {                
                ViewBag.errorCode = obj.errorCode;
                ViewBag.errorMessage = obj.message;                
                return View("error");
            }
        }

        private string document1(string signerEmail, string signerName, string ccEmail, string ccName)
        {
            return " <!DOCTYPE html>\n" +
                    "    <html>\n" +
                    "        <head>\n" +
                    "          <meta charset=\"UTF-8\">\n" +
                    "        </head>\n" +
                    "        <body style=\"font-family:sans-serif;margin-left:2em;\">\n" +
                    "        <h1 style=\"font-family: 'Trebuchet MS', Helvetica, sans-serif;\n" +
                    "            color: darkblue;margin-bottom: 0;\">World Wide Corp</h1>\n" +
                    "        <h2 style=\"font-family: 'Trebuchet MS', Helvetica, sans-serif;\n" +
                    "          margin-top: 0px;margin-bottom: 3.5em;font-size: 1em;\n" +
                    "          color: darkblue;\">Order Processing Division</h2>\n" +
                    "        <h4>Ordered by " + signerName + "</h4>\n" +
                    "        <p style=\"margin-top:0em; margin-bottom:0em;\">Email: " + signerEmail + "</p>\n" +
                    "        <p style=\"margin-top:0em; margin-bottom:0em;\">Copy to: " + ccName + ", " + ccEmail + "</p>\n" +
                    "        <p style=\"margin-top:3em;\">\n" +
                    "  Candy bonbon pastry jujubes lollipop wafer biscuit biscuit. Topping brownie sesame snaps sweet roll pie. Croissant danish biscuit soufflé caramels jujubes jelly. Dragée danish caramels lemon drops dragée. Gummi bears cupcake biscuit tiramisu sugar plum pastry. Dragée gummies applicake pudding liquorice. Donut jujubes oat cake jelly-o. Dessert bear claw chocolate cake gummies lollipop sugar plum ice cream gummies cheesecake.\n" +
                    "        </p>\n" +
                    "        <!-- Note the anchor tag for the signature field is in white. -->\n" +
                    "        <h3 style=\"margin-top:3em;\">Agreed: <span style=\"color:white;\">**signature_1**/</span></h3>\n" +
                    "        </body>\n" +
                    "    </html>";
        }

        private object MakeEnvelope(string signerEmail, string signerName, string ccEmail, string ccName)
        {
            // document 1 (html) has tag **signature_1**
            // document 2 (docx) has tag /sn1/
            // document 3 (pdf) has tag /sn1/
            //
            // The envelope has two recipients.
            // recipient 1 - signer
            // recipient 2 - cc
            // The envelope will be sent first to the signer.
            // After it is signed, a copy is sent to the cc person.
            // create the envelope definition
            // add the documents
            var doc1 = new
            {
                name = "Order acknowledgement", // can be different from actual file name
                fileExtension = "html", // Source data format. Signed docs are always pdf.
                documentId = "1" // a label used to reference the doc
            };

            var doc2 = new
            {
                name = "Battle Plan", // can be different from actual file name
                fileExtension = "docx",
                documentId = "2"
            };
            var doc3 = new
            {
                name = "Lorem Ipsum", // can be different from actual file name
                fileExtension = "pdf",
                documentId = "3"
            };

            // create the envelope definition
            //env.Documents = new [] { doc1, doc2, doc3 };

            // create a signer recipient to sign the document, identified by name and email
            // We're setting the parameters via the object creation
            Signer signer1 = new Signer();
            signer1.Email = signerEmail;
            signer1.Name = signerName;
            signer1.RecipientId = "1";
            signer1.RoutingOrder = "1";
            // routingOrder (lower means earlier) determines the order of deliveries
            // to the recipients. Parallel routing order is supported by using the
            // same integer as the order for two or more recipients.

            // create a cc recipient to receive a copy of the documents, identified by name and email
            // We're setting the parameters via setters
            CarbonCopy cc1 = new CarbonCopy();
            cc1.Email = ccEmail;
            cc1.Name = ccName;
            cc1.RoutingOrder = "2";
            cc1.RecipientId = "2";
            // Create signHere fields (also known as tabs) on the documents,
            // We're using anchor (autoPlace) positioning
            //
            // The DocuSign platform searches throughout your envelope's
            // documents for matching anchor strings. So the
            // signHere2 tab will be used in both document 2 and 3 since they
            // use the same anchor string for their "signer 1" tabs.
            SignHere signHere1 = new SignHere();
            signHere1.AnchorString = "**signature_1**";
            signHere1.AnchorYOffset = "10";
            signHere1.AnchorUnits = "pixels";
            signHere1.AnchorXOffset = "20";
            SignHere signHere2 = new SignHere();
            signHere2.AnchorString = "/sn1/";
            signHere2.AnchorYOffset = "10";
            signHere2.AnchorUnits = "pixels";
            signHere2.AnchorXOffset = "20";

            // Tabs are set per recipient / signer
            Tabs signer1Tabs = new Tabs();
            signer1Tabs.SignHereTabs = new List<SignHere> { signHere1, signHere2 };
            signer1.Tabs = signer1Tabs;

            // Add the recipients to the envelope object
            Recipients recipients = new Recipients();
            recipients.Signers = new List<Signer> { signer1 };
            recipients.CarbonCopies = new List<CarbonCopy> { cc1 };


            dynamic env = new
            {
                emailSubject = "Please sign this document set",
                documents = new[] { doc1, doc2, doc3 },
                recipients = recipients,
                // Request that the envelope be sent by setting |status| to "sent".
                // To request that the envelope be created as a draft, set to "created"
                status = "sent"
            };

            //env.Status = "sent";

            return env;
        }
    }
}