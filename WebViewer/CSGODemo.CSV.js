function sortData(attribute, ascending)
{
  sortDataNew(attribute.innerHTML, ascending);
}

var datatable;

function sortDataNew(heading, ascending = true)
{
  var headings = [];
  for(var i = 0; i < datatable.rows[0].cells.length; i++)
    headings.push(datatable.rows[0].cells[i].innerHTML);

  var index = headings.indexOf(heading);
  var store = [];
  for(var i=0, len=datatable.rows.length; i<len; i++){
      var row = datatable.rows[i];
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
      datatable.appendChild(store[i][1]);
  }
  store = null;
}

document.addEventListener("DOMContentLoaded", function(event) {
  datatable = document.getElementById('csv_target');

  //Add up/down handlers for each headerRow
  var headerCells = [];
  for(var i = 0; i < datatable.rows[0].cells.length; i++)
  {
    datatable.rows[0].cells[i].onclick = function () { sortData(this, false); };
  }
  
});
