
function postRequest() {
    var form = document.getElementById("advform"); 

    var xhr = new XMLHttpRequest();
    var data = new FormData(form);

    xhr.onload = function () {
        console.log(this.response)
        console.log(this.responseText);
        if (this.status == 200) {
            var obj = JSON.parse(this.responseText)

            document.getElementById("response").innerHTML = "Pay this invoice: " + obj.invoice
        } else if (this.status == 400) {
            var obj = JSON.parse(this.responseText)
            document.getElementById("response").innerHTML = "ERROR: "+ obj.error
        } else {
            document.getElementById("response").innerHTML = "response: " + this.status + ";" + this.responseText;
        }
        
    }

    xhr.open("post", "https://ads.donnerlab.com/ads/upload");
    xhr.send(data);
}
