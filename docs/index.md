---
documentType: index
title: Akka.Persistence.Sql Documentation
---

<!-- markdownlint-disable MD033 -->
<!-- markdownlint-disable MD009 -->
<style>
.subtitle {
    font-size:20px;
}
.jumbotron{
    text-align: center;
}
img.main-logo{
    width: 192px;
}
img.pb-logo-full{
    width:  200px;
}
h2:before{
    display: none;
}
.featured-box-minimal h4:before {
    height: 0px;
    margin-top: 0px;
}
</style>

<div class="container">
    <div class="jumbotron">   
      <img src="images/mainlogo.png" class="main-logo" />
      <h1 class="title">Akka.Persistence.Sql</h1>
      <h1 class="title"><small class="subtitle">A Cross-SQL-DB Engine Akka.Persistence plugin with broad database compatibility.</small></h1>
    </div>
</div>

<section>
    <div class="container">
        <h2 class="lead">A Cross-SQL-DB Engine Akka.Persistence plugin with broad database compatibility thanks to <a href="https://linq2db.github.io/">Linq2Db</a>.</h2>
        <p class="lead">This is a port of the amazing <a href="https://github.com/akka/akka-persistence-jdbc">akka-persistence-jdbc</a> package from Scala, with a few improvements based on C# as well as our choice of data library.</p>
    </div>
</section>

<section>
    <div class="container">
        <div class="alert-info" style="padding: 15px">
            <h3>This Is Still a Beta</h3>
            <p>Please note this is still considered 'work in progress' and only used if one understands the risks. While the TCK Specs pass you should still test in a 'safe' non-production environment carefully before deciding to fully deploy.</p>
        </div>
        <br/>
        <div class="alert-info" style="padding: 15px">
            <h3>Suitable For Greenfield Projects Only</h3>
            <p>Until backward compatibility is properly tested and documented, it is recommended to use this plugin only on new greenfield projects that does not rely on existing persisted data.</p>
        </div>
    </div>
</section>