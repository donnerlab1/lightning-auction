
function postRequest() {
    var form = document.getElementById("advform"); 

    var xhr = new XMLHttpRequest();
    var data = new FormData(form);

    xhr.onload = function () {
        console.log(this.response)
        console.log(this.responseText);

        document.getElementById("response").innerHTML = "response: " + this.status+";"+ this.responseText;
    }

    xhr.open("post", "http://localhost:8012/ads/upload");
    xhr.send(data);
}
