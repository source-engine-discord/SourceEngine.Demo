class csvViewer
{
  constructor(data)
  {
    var lines = data.split('\n');
    this.headers = lines[0].split(',');
    this.rows = [];
    for(var i = 1; i < lines.length; i++)
    {
      var elems = lines[i].split(',');
      if(elems.length == this.headers.length)
      this.rows.push(elems);
    }

    //CREATE TABLE
    //===================================================
    this.table = document.createElement("table");
    target.appendChild(this.table);
    this.table.setAttribute("border", "2");

    this.render();

    return;

    var tableTarget = document.getElementById("target");

    //Create table


    var headerRow = document.createElement("tr");

    for(var x = 0; x < this.headers.length; x++)
    {
      var cell = document.createElement("td");
      var cellText = document.createTextNode(this.headers[x]);
      cell.appendChild(cellText);
      cell.onclick = function () { sortData(this); };
      headerRow.appendChild(cell);
    }

    this.tableBody.appendChild(headerRow);

    for(var i = 1; i < lines.length; i++)
    {
      //Split CSV
      var elements = lines[i].split(',');

      if(elements.length == this.headers.length)
      {
        var row = document.createElement("tr");



        for(var x = 0; x < elements.length; x++)
        {
          var cell = document.createElement("td");
          var cellText = document.createTextNode(elements[x]);
          cell.appendChild(cellText);

          row.appendChild(cell);
        }

        this.tableBody.appendChild(row);
      }
    }




  }

  render()
  {
    //DRAW THE HEADER ROW
    var headerRow = document.createElement("tr");
    for(var x = 0; x < this.headers.length; x++)
    {
      var cell = document.createElement("td");
      var cellText = document.createTextNode(this.headers[x]);
      cell.appendChild(cellText);
      cell.onclick = function () { sortData(this); };
      headerRow.appendChild(cell);
    }


    this.tableBody = document.createElement("tbody");
    this.tableBody.appendChild(headerRow);
    this.table.appendChild(this.tableBody);

    for(var i = 0; i < this.rows.length; i++)
    {
      var row = document.createElement("tr");

      for(var x = 0; x < this.headers.length; x++)
      {
        var cell = document.createElement("td");
        var cellText = document.createTextNode(this.rows[i][x]);
        cell.appendChild(cellText);

        row.appendChild(cell);
      }

      this.tableBody.appendChild(row);
    }
  }


  sortTableByAttribute(attribute, ascending = true){
     var index = this.headers.indexOf(attribute);
     var tbl = this.table;
     var store = [];
     for(var i=0, len=tbl.rows.length; i<len; i++){
         var row = tbl.rows[i];
         var sortnr = parseFloat(row.cells[index].textContent || row.cells[index].innerText);
         if(!isNaN(sortnr)) store.push([sortnr, row]);
     }
     store.sort(function(x,y){
       if(ascending)
         return x[0] - y[0];
       else
         return y[0] - x[0];
     });
     for(var i=0, len=store.length; i<len; i++){
         tbl.appendChild(store[i][1]);
     }
     store = null;
  }
}

var csv;

function loadData(file)
{
  var client = new XMLHttpRequest();
  client.open('GET', file);
  client.onreadystatechange = function() {

    if(client.readyState == 4)
    {
      csv = new csvViewer(client.response);
    }
  }
  client.send();
}

function sortData(attribute)
{
  csv.sortTableByAttribute(attribute.innerHTML);
}

document.addEventListener("DOMContentLoaded", function(event) {
  loadData('/data.csv');
});
